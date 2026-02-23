using System;
using System.Security.Cryptography;
using System.Text;

namespace TryCr4ckP4ss.Services;

public class EncryptionService
{
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int LegacyIvSize = 16;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 310000;
    private const int LegacyIterations = 100000;
    private const byte CurrentVersion = 2;
    private static readonly byte[] VaultMagic = "TCPV"u8.ToArray();
    private static readonly byte[] AesGcmAssociatedData = "TRYCR4CKP4SS_V2"u8.ToArray();

    public byte[] EncryptVault(string plaintext, string masterPassword, byte[]? existingSalt = null)
    {
        var salt = existingSalt ?? RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(masterPassword, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        try
        {
            using var aesGcm = new AesGcm(key, TagSize);
            aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag, AesGcmAssociatedData);
            return CombineV2(salt, nonce, tag, ciphertext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }

    public string DecryptVault(byte[] encryptedData, string masterPassword, out byte[] salt)
    {
        if (TrySplitV2(encryptedData, out var parsedSalt, out var nonce, out var tag, out var ciphertext))
        {
            salt = parsedSalt;
            return DecryptV2(parsedSalt, nonce, tag, ciphertext, masterPassword);
        }

        var (legacySalt, legacyIv, legacyCiphertext) = SplitLegacy(encryptedData);
        salt = legacySalt;
        return DecryptLegacy(legacySalt, legacyIv, legacyCiphertext, masterPassword);
    }

    private string DecryptV2(byte[] salt, byte[] nonce, byte[] tag, byte[] ciphertext, string masterPassword)
    {
        var key = Rfc2898DeriveBytes.Pbkdf2(masterPassword, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        var plaintextBytes = new byte[ciphertext.Length];

        try
        {
            using var aesGcm = new AesGcm(key, TagSize);
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintextBytes, AesGcmAssociatedData);
            return Encoding.UTF8.GetString(plaintextBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }

    private string DecryptLegacy(byte[] salt, byte[] iv, byte[] ciphertext, string masterPassword)
    {
        var key = Rfc2898DeriveBytes.Pbkdf2(masterPassword, salt, LegacyIterations, HashAlgorithmName.SHA256, KeySize);
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var plaintextBytes = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        try
        {
            return Encoding.UTF8.GetString(plaintextBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }

    private static byte[] CombineV2(byte[] salt, byte[] nonce, byte[] tag, byte[] ciphertext)
    {
        var headerLength = VaultMagic.Length + 1 + salt.Length + nonce.Length + tag.Length;
        var combined = new byte[headerLength + ciphertext.Length];
        var offset = 0;

        Buffer.BlockCopy(VaultMagic, 0, combined, offset, VaultMagic.Length);
        offset += VaultMagic.Length;
        combined[offset] = CurrentVersion;
        offset += 1;

        Buffer.BlockCopy(salt, 0, combined, offset, salt.Length);
        offset += salt.Length;
        Buffer.BlockCopy(nonce, 0, combined, offset, nonce.Length);
        offset += nonce.Length;
        Buffer.BlockCopy(tag, 0, combined, offset, tag.Length);
        offset += tag.Length;
        Buffer.BlockCopy(ciphertext, 0, combined, offset, ciphertext.Length);
        return combined;
    }

    private static bool TrySplitV2(byte[] combined, out byte[] salt, out byte[] nonce, out byte[] tag, out byte[] ciphertext)
    {
        salt = [];
        nonce = [];
        tag = [];
        ciphertext = [];

        var minLength = VaultMagic.Length + 1 + SaltSize + NonceSize + TagSize + 1;
        if (combined.Length < minLength)
        {
            return false;
        }

        for (var i = 0; i < VaultMagic.Length; i++)
        {
            if (combined[i] != VaultMagic[i])
            {
                return false;
            }
        }

        if (combined[VaultMagic.Length] != CurrentVersion)
        {
            return false;
        }

        var offset = VaultMagic.Length + 1;
        salt = new byte[SaltSize];
        Buffer.BlockCopy(combined, offset, salt, 0, SaltSize);
        offset += SaltSize;

        nonce = new byte[NonceSize];
        Buffer.BlockCopy(combined, offset, nonce, 0, NonceSize);
        offset += NonceSize;

        tag = new byte[TagSize];
        Buffer.BlockCopy(combined, offset, tag, 0, TagSize);
        offset += TagSize;

        ciphertext = new byte[combined.Length - offset];
        Buffer.BlockCopy(combined, offset, ciphertext, 0, ciphertext.Length);
        return true;
    }

    private static (byte[] salt, byte[] iv, byte[] ciphertext) SplitLegacy(byte[] combined)
    {
        if (combined.Length <= SaltSize + LegacyIvSize)
        {
            throw new CryptographicException("Vault payload is invalid.");
        }

        var salt = new byte[SaltSize];
        var iv = new byte[LegacyIvSize];
        var ciphertext = new byte[combined.Length - SaltSize - LegacyIvSize];

        Buffer.BlockCopy(combined, 0, salt, 0, SaltSize);
        Buffer.BlockCopy(combined, SaltSize, iv, 0, LegacyIvSize);
        Buffer.BlockCopy(combined, SaltSize + LegacyIvSize, ciphertext, 0, ciphertext.Length);

        return (salt, iv, ciphertext);
    }
}
