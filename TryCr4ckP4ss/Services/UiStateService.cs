using System;
using System.IO;
using System.Text.Json;

namespace TryCr4ckP4ss.Services;

public sealed class UiStateService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _statePath;

    public UiStateService(string statePath)
    {
        _statePath = statePath;
    }

    public UiState Load()
    {
        try
        {
            if (!File.Exists(_statePath))
            {
                return new UiState();
            }

            var json = File.ReadAllText(_statePath);
            return JsonSerializer.Deserialize<UiState>(json) ?? new UiState();
        }
        catch
        {
            return new UiState();
        }
    }

    public void Save(UiState state)
    {
        try
        {
            var directory = Path.GetDirectoryName(_statePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(state, JsonOptions);
            var tempPath = $"{_statePath}.{Guid.NewGuid():N}.tmp";
            File.WriteAllText(tempPath, json);

            if (File.Exists(_statePath))
            {
                try
                {
                    File.Replace(tempPath, _statePath, null, true);
                }
                catch
                {
                    File.Delete(_statePath);
                    File.Move(tempPath, _statePath);
                }
            }
            else
            {
                File.Move(tempPath, _statePath);
            }
        }
        catch
        {
        }
    }
}

public sealed class UiState
{
    public string SelectedCategory { get; set; } = "all";
    public bool FavoritesOnly { get; set; }
    public string SelectedSort { get; set; } = "Recent";
    public int AutoLockMinutes { get; set; } = 5;
    public bool RequireReauthForSensitiveActions { get; set; } = true;
    public bool UseTokyoWhiteTheme { get; set; }
}
