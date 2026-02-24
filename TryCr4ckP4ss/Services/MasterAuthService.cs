using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TryCr4ckP4ss.Services;

public sealed class MasterAuthService
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int Iterations = 200_000;
    private static readonly byte[] RecoveryAssociatedData = "TCP_MASTER_RECOVERY_V1"u8.ToArray();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _authPath;
    private MasterAuthProfile? _profile;

    public MasterAuthService(string authPath)
    {
        _authPath = authPath;
        _profile = LoadProfile();
    }

    public bool HasProfile => _profile != null;

    public static bool TryNormalizeSecurityAnswer(string? answer, out string normalized)
    {
        normalized = (answer ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return false;
        }

        foreach (var ch in normalized)
        {
            if (ch is < 'a' or > 'z')
            {
                return false;
            }
        }

        return true;
    }

    public bool SaveProfile(string masterPassword, string securityAnswer)
    {
        if (!TryNormalizeSecurityAnswer(securityAnswer, out var normalizedAnswer))
        {
            return false;
        }

        try
        {
            var masterSalt = RandomNumberGenerator.GetBytes(SaltSize);
            var answerSalt = RandomNumberGenerator.GetBytes(SaltSize);
            var recoverySalt = RandomNumberGenerator.GetBytes(SaltSize);
            var recoveryNonce = RandomNumberGenerator.GetBytes(NonceSize);
            var masterHash = DeriveHash(masterPassword ?? string.Empty, masterSalt);
            var answerHash = DeriveHash(normalizedAnswer, answerSalt);
            var recoveryKey = DeriveHash(normalizedAnswer, recoverySalt);
            var masterPasswordBytes = Encoding.UTF8.GetBytes(masterPassword ?? string.Empty);
            var recoveryCiphertext = new byte[masterPasswordBytes.Length];
            var recoveryTag = new byte[TagSize];

            try
            {
                using var aesGcm = new AesGcm(recoveryKey, TagSize);
                aesGcm.Encrypt(recoveryNonce, masterPasswordBytes, recoveryCiphertext, recoveryTag, RecoveryAssociatedData);

                var profile = new MasterAuthProfile
                {
                    Version = 1,
                    MasterPasswordSalt = Convert.ToBase64String(masterSalt),
                    MasterPasswordHash = Convert.ToBase64String(masterHash),
                    SecurityAnswerSalt = Convert.ToBase64String(answerSalt),
                    SecurityAnswerHash = Convert.ToBase64String(answerHash),
                    RecoverySalt = Convert.ToBase64String(recoverySalt),
                    RecoveryNonce = Convert.ToBase64String(recoveryNonce),
                    RecoveryTag = Convert.ToBase64String(recoveryTag),
                    RecoveryCiphertext = Convert.ToBase64String(recoveryCiphertext)
                };

                var directory = Path.GetDirectoryName(_authPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(profile, JsonOptions);
                var tempPath = $"{_authPath}.{Guid.NewGuid():N}.tmp";
                File.WriteAllText(tempPath, json);
                TryRestrictFilePermissions(tempPath);

                if (File.Exists(_authPath))
                {
                    try
                    {
                        File.Replace(tempPath, _authPath, null, true);
                    }
                    catch
                    {
                        File.Delete(_authPath);
                        File.Move(tempPath, _authPath);
                    }
                }
                else
                {
                    File.Move(tempPath, _authPath);
                }

                _profile = profile;
                return true;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(masterHash);
                CryptographicOperations.ZeroMemory(answerHash);
                CryptographicOperations.ZeroMemory(recoveryKey);
                CryptographicOperations.ZeroMemory(masterPasswordBytes);
                CryptographicOperations.ZeroMemory(recoveryCiphertext);
                CryptographicOperations.ZeroMemory(recoveryTag);
            }
        }
        catch
        {
            return false;
        }
    }

    public bool VerifyMasterPassword(string candidate)
    {
        if (_profile == null)
        {
            return false;
        }

        return VerifySecret(candidate ?? string.Empty, _profile.MasterPasswordSalt, _profile.MasterPasswordHash);
    }

    public bool VerifySecurityAnswer(string candidate)
    {
        if (_profile == null || !TryNormalizeSecurityAnswer(candidate, out var normalized))
        {
            return false;
        }

        return VerifySecret(normalized, _profile.SecurityAnswerSalt, _profile.SecurityAnswerHash);
    }

    public bool TryRecoverMasterPassword(string securityAnswer, out string recoveredMasterPassword)
    {
        recoveredMasterPassword = string.Empty;
        if (_profile == null || !TryNormalizeSecurityAnswer(securityAnswer, out var normalizedAnswer))
        {
            return false;
        }

        if (!VerifySecret(normalizedAnswer, _profile.SecurityAnswerSalt, _profile.SecurityAnswerHash) ||
            string.IsNullOrWhiteSpace(_profile.RecoverySalt) ||
            string.IsNullOrWhiteSpace(_profile.RecoveryNonce) ||
            string.IsNullOrWhiteSpace(_profile.RecoveryTag) ||
            string.IsNullOrWhiteSpace(_profile.RecoveryCiphertext))
        {
            return false;
        }

        try
        {
            var recoverySalt = Convert.FromBase64String(_profile.RecoverySalt);
            var nonce = Convert.FromBase64String(_profile.RecoveryNonce);
            var tag = Convert.FromBase64String(_profile.RecoveryTag);
            var ciphertext = Convert.FromBase64String(_profile.RecoveryCiphertext);
            var key = DeriveHash(normalizedAnswer, recoverySalt);
            var plaintext = new byte[ciphertext.Length];

            try
            {
                using var aesGcm = new AesGcm(key, TagSize);
                aesGcm.Decrypt(nonce, ciphertext, tag, plaintext, RecoveryAssociatedData);
                recoveredMasterPassword = Encoding.UTF8.GetString(plaintext);
                return true;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
        catch
        {
            return false;
        }
    }

    private MasterAuthProfile? LoadProfile()
    {
        try
        {
            if (!File.Exists(_authPath))
            {
                return null;
            }

            var json = File.ReadAllText(_authPath);
            var profile = JsonSerializer.Deserialize<MasterAuthProfile>(json);
            if (profile == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(profile.MasterPasswordSalt) ||
                string.IsNullOrWhiteSpace(profile.MasterPasswordHash) ||
                string.IsNullOrWhiteSpace(profile.SecurityAnswerSalt) ||
                string.IsNullOrWhiteSpace(profile.SecurityAnswerHash))
            {
                return null;
            }

            return profile;
        }
        catch
        {
            return null;
        }
    }

    private static bool VerifySecret(string secret, string saltBase64, string expectedHashBase64)
    {
        try
        {
            var salt = Convert.FromBase64String(saltBase64);
            var expectedHash = Convert.FromBase64String(expectedHashBase64);
            var candidateHash = DeriveHash(secret, salt);

            try
            {
                return candidateHash.Length == expectedHash.Length &&
                       CryptographicOperations.FixedTimeEquals(candidateHash, expectedHash);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(candidateHash);
            }
        }
        catch
        {
            return false;
        }
    }

    private static byte[] DeriveHash(string secret, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(secret, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
    }

    private static void TryRestrictFilePermissions(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
        }
    }

    private sealed class MasterAuthProfile
    {
        public int Version { get; set; } = 1;
        public string MasterPasswordSalt { get; set; } = string.Empty;
        public string MasterPasswordHash { get; set; } = string.Empty;
        public string SecurityAnswerSalt { get; set; } = string.Empty;
        public string SecurityAnswerHash { get; set; } = string.Empty;
        public string RecoverySalt { get; set; } = string.Empty;
        public string RecoveryNonce { get; set; } = string.Empty;
        public string RecoveryTag { get; set; } = string.Empty;
        public string RecoveryCiphertext { get; set; } = string.Empty;
    }
}
