using System;
using System.Security.Cryptography;
using System.Text;

namespace TryCr4ckP4ss.Services;

public class EncryptionService
{
    private const int SaltSize = 16;
    private const int IvSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100000;

    public (byte[] salt, byte[] iv, byte[] ciphertext) Encrypt(string plaintext, string masterPassword, byte[]? existingSalt = null)
    {
        var salt = existingSalt ?? RandomNumberGenerator.GetBytes(SaltSize);
        var iv = RandomNumberGenerator.GetBytes(IvSize);
        
        var key = Rfc2898DeriveBytes.Pbkdf2(masterPassword, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        CryptographicOperations.ZeroMemory(key);
        
        return (salt, iv, ciphertext);
    }

    public string Decrypt(byte[] salt, byte[] iv, byte[] ciphertext, string masterPassword)
    {
        var key = Rfc2898DeriveBytes.Pbkdf2(masterPassword, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var plaintextBytes = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        
        CryptographicOperations.ZeroMemory(key);
        
        return Encoding.UTF8.GetString(plaintextBytes);
    }

    public static byte[] Combine(byte[] salt, byte[] iv, byte[] ciphertext)
    {
        var combined = new byte[salt.Length + iv.Length + ciphertext.Length];
        Buffer.BlockCopy(salt, 0, combined, 0, salt.Length);
        Buffer.BlockCopy(iv, 0, combined, salt.Length, iv.Length);
        Buffer.BlockCopy(ciphertext, 0, combined, salt.Length + iv.Length, ciphertext.Length);
        return combined;
    }

    public static (byte[] salt, byte[] iv, byte[] ciphertext) Split(byte[] combined)
    {
        var salt = new byte[SaltSize];
        var iv = new byte[IvSize];
        var ciphertext = new byte[combined.Length - SaltSize - IvSize];
        
        Buffer.BlockCopy(combined, 0, salt, 0, SaltSize);
        Buffer.BlockCopy(combined, SaltSize, iv, 0, IvSize);
        Buffer.BlockCopy(combined, SaltSize + IvSize, ciphertext, 0, ciphertext.Length);
        
        return (salt, iv, ciphertext);
    }
}
