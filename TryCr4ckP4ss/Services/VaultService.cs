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
            var (salt, iv, ciphertext) = EncryptionService.Split(encryptedData);
            
            var decryptedJson = _encryptionService.Decrypt(salt, iv, ciphertext, masterPassword);
            
            _salt = salt;
            _credentials = JsonSerializer.Deserialize<List<Credential>>(decryptedJson) ?? [];
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
        var json = JsonSerializer.Serialize(_credentials, new JsonSerializerOptions { WriteIndented = true });
        var (salt, iv, ciphertext) = _encryptionService.Encrypt(json, masterPassword, _salt);
        
        _salt ??= salt;
        
        var combined = EncryptionService.Combine(_salt, iv, ciphertext);
        
        var directory = Path.GetDirectoryName(_vaultPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        File.WriteAllBytes(_vaultPath, combined);
    }

    public void AddCredential(Credential credential)
    {
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
        existing.Category = credential.Category;
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
        return _credentials.OrderByDescending(c => c.IsFavorite).ThenByDescending(c => c.ModifiedAt).ToList();
    }

    public List<Credential> GetFavorites()
    {
        return _credentials.Where(c => c.IsFavorite).OrderByDescending(c => c.ModifiedAt).ToList();
    }

    public List<Credential> GetByCategory(string category)
    {
        return _credentials.Where(c => c.Category == category).OrderByDescending(c => c.ModifiedAt).ToList();
    }

    public List<Credential> SearchCredentials(string searchTerm, string? category = null)
    {
        var query = _credentials.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(category) && category != "all")
        {
            query = query.Where(c => c.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(c => 
                c.Site.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                c.Username.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                c.Url.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        }

        return query.OrderByDescending(c => c.IsFavorite).ThenByDescending(c => c.ModifiedAt).ToList();
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
}
