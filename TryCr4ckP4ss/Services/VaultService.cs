using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using TryCr4ckP4ss.Models;

namespace TryCr4ckP4ss.Services;

public class VaultService
{
    private const int SaltSize = 16;
    private static readonly JsonSerializerOptions VaultJsonOptions = new() { WriteIndented = true };

    private readonly EncryptionService _encryptionService;
    private readonly string _vaultPath;
    private byte[]? _salt;
    private List<Credential> _credentials = [];

    public VaultService(string vaultPath)
    {
        _encryptionService = new EncryptionService();
        _vaultPath = vaultPath;
    }

    public bool Initialize(string masterPassword)
    {
        if (File.Exists(_vaultPath))
        {
            return LoadVault(masterPassword);
        }
        
        _salt = null;
        _credentials = [];
        return true;
    }

    private bool LoadVault(string masterPassword)
    {
        try
        {
            var encryptedData = File.ReadAllBytes(_vaultPath);
            var decryptedJson = _encryptionService.DecryptVault(encryptedData, masterPassword, out var salt);

            _salt = salt;
            _credentials = JsonSerializer.Deserialize<List<Credential>>(decryptedJson) ?? [];
            NormalizeCredentials();
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    public void SaveVault(string masterPassword)
    {
        _salt ??= RandomNumberGenerator.GetBytes(SaltSize);
        var json = JsonSerializer.Serialize(_credentials, VaultJsonOptions);
        var encryptedData = _encryptionService.EncryptVault(json, masterPassword, _salt);

        var directory = Path.GetDirectoryName(_vaultPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{_vaultPath}.{Guid.NewGuid():N}.tmp";
        File.WriteAllBytes(tempPath, encryptedData);
        TryRestrictFilePermissions(tempPath);

        if (File.Exists(_vaultPath))
        {
            try
            {
                File.Replace(tempPath, _vaultPath, null, true);
            }
            catch
            {
                File.Delete(_vaultPath);
                File.Move(tempPath, _vaultPath);
            }
        }
        else
        {
            File.Move(tempPath, _vaultPath);
        }
    }

    public void AddCredential(Credential credential)
    {
        credential.Category = NormalizeCategory(credential.Category);
        credential.TotpSecret = NormalizeTotpSecret(credential.TotpSecret);
        credential.CreatedAt = DateTime.Now;
        credential.ModifiedAt = DateTime.Now;
        _credentials.Insert(0, credential);
    }

    public bool UpdateCredential(Credential credential)
    {
        var existing = _credentials.FirstOrDefault(c => c.Id == credential.Id);
        if (existing == null) return false;
        
        existing.Site = credential.Site;
        existing.Username = credential.Username;
        existing.Password = credential.Password;
        existing.Url = credential.Url;
        existing.Notes = credential.Notes;
        existing.Category = NormalizeCategory(credential.Category);
        existing.TotpSecret = NormalizeTotpSecret(credential.TotpSecret);
        existing.IsFavorite = credential.IsFavorite;
        existing.ModifiedAt = DateTime.Now;
        return true;
    }

    public bool DeleteCredential(string id)
    {
        var credential = _credentials.FirstOrDefault(c => c.Id == id);
        if (credential == null) return false;
        
        _credentials.Remove(credential);
        return true;
    }

    public Credential? GetById(string id)
    {
        return _credentials.FirstOrDefault(c => c.Id == id);
    }

    public List<Credential> GetAllCredentials()
    {
        return ApplySort(_credentials, "recent").ToList();
    }

    public List<Credential> GetFavorites()
    {
        return ApplySort(_credentials.Where(c => c.IsFavorite), "recent").ToList();
    }

    public List<Credential> GetByCategory(string category)
    {
        return ApplySort(_credentials.Where(c => string.Equals(c.Category, NormalizeCategory(category), StringComparison.OrdinalIgnoreCase)), "recent").ToList();
    }

    public List<Credential> SearchCredentials(string searchTerm, string? category = null, bool favoritesOnly = false, string sortBy = "recent")
    {
        var query = _credentials.AsEnumerable();
        var normalizedCategory = NormalizeCategory(category);

        if (favoritesOnly || normalizedCategory == "favorites")
        {
            query = query.Where(c => c.IsFavorite);
        }

        if (!string.IsNullOrWhiteSpace(category) &&
            !string.Equals(category, "all", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(category, "favorites", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(c => string.Equals(c.Category, normalizedCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(c =>
                c.Site.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                c.Username.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                c.Url.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                c.Notes.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                c.Category.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        }

        return ApplySort(query, sortBy).ToList();
    }

    public void ToggleFavorite(string id)
    {
        var credential = _credentials.FirstOrDefault(c => c.Id == id);
        if (credential != null)
        {
            credential.IsFavorite = !credential.IsFavorite;
            credential.ModifiedAt = DateTime.Now;
        }
    }

    public bool VaultExists => File.Exists(_vaultPath);
    
    public int Count => _credentials.Count;

    public HashSet<string> GetReusedPasswordIds()
    {
        return _credentials
            .Where(c => !string.IsNullOrWhiteSpace(c.Password))
            .GroupBy(c => c.Password, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Select(c => c.Id))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<Credential> ApplySort(IEnumerable<Credential> query, string sortBy)
    {
        return sortBy.ToLowerInvariant() switch
        {
            "site" => query
                .OrderBy(c => c.Site, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(c => c.ModifiedAt),
            "weak" => query
                .OrderBy(c => PasswordHealthService.CalculateScore(c.Password))
                .ThenBy(c => c.Site, StringComparer.OrdinalIgnoreCase),
            _ => query
                .OrderByDescending(c => c.IsFavorite)
                .ThenByDescending(c => c.ModifiedAt)
        };
    }

    private static string NormalizeCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return "general";
        }

        return category.Trim().ToLowerInvariant();
    }

    private static string NormalizeTotpSecret(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return string.Empty;
        }

        return secret.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
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

    private void NormalizeCredentials()
    {
        foreach (var credential in _credentials)
        {
            credential.Site ??= string.Empty;
            credential.Username ??= string.Empty;
            credential.Password ??= string.Empty;
            credential.Url ??= string.Empty;
            credential.Notes ??= string.Empty;
            credential.Category = NormalizeCategory(credential.Category);
            credential.TotpSecret = NormalizeTotpSecret(credential.TotpSecret);

            if (credential.CreatedAt == default)
            {
                credential.CreatedAt = DateTime.Now;
            }

            if (credential.ModifiedAt == default)
            {
                credential.ModifiedAt = credential.CreatedAt;
            }
        }
    }
}
