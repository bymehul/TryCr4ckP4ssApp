using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TryCr4ckP4ss.Models;
using TryCr4ckP4ss.Services;

namespace TryCr4ckP4ss.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private static readonly TimeSpan SensitiveActionReauthWindow = TimeSpan.FromSeconds(45);

    private readonly VaultService _vaultService;
    private readonly UiStateService _uiStateService;
    private string _masterPassword = string.Empty;
    private CancellationTokenSource? _sessionLoopCts;
    private DateTimeOffset _lastActivityUtc = DateTimeOffset.UtcNow;
    private DateTimeOffset _lastReauthUtc = DateTimeOffset.MinValue;
    private Action? _pendingSensitiveAction;
    private int _statusSequence;
    private bool _isApplyingUiState;

    [ObservableProperty]
    private bool _isAuthenticated;

    [ObservableProperty]
    private bool _isLoginFailed;

    [ObservableProperty]
    private string _loginError = string.Empty;

    [ObservableProperty]
    private string _loginPassword = string.Empty;

    [ObservableProperty]
    private string _searchTerm = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "all";

    [ObservableProperty]
    private bool _favoritesOnly;

    [ObservableProperty]
    private string _selectedSort = "Recent";

    [ObservableProperty]
    private CredentialViewModel? _selectedCredential;

    [ObservableProperty]
    private bool _showAddDialog;

    [ObservableProperty]
    private bool _showEditDialog;

    [ObservableProperty]
    private bool _showDetailPanel;

    [ObservableProperty]
    private bool _showReauthDialog;

    [ObservableProperty]
    private string _reauthPassword = string.Empty;

    [ObservableProperty]
    private string _reauthError = string.Empty;

    [ObservableProperty]
    private string _newSite = string.Empty;

    [ObservableProperty]
    private string _newUsername = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _newUrl = string.Empty;

    [ObservableProperty]
    private string _newNotes = string.Empty;

    [ObservableProperty]
    private string _newCategory = "general";

    [ObservableProperty]
    private string _newTotpSecret = string.Empty;

    [ObservableProperty]
    private bool _newIsFavorite;

    [ObservableProperty]
    private bool _showPasswordInDetail;

    [ObservableProperty]
    private string _generatedPassword = string.Empty;

    [ObservableProperty]
    private int _passwordLength = 16;

    [ObservableProperty]
    private bool _includeLowercase = true;

    [ObservableProperty]
    private bool _includeUppercase = true;

    [ObservableProperty]
    private bool _includeNumbers = true;

    [ObservableProperty]
    private bool _includeSymbols = true;

    [ObservableProperty]
    private int _autoLockMinutes = 5;

    [ObservableProperty]
    private bool _requireReauthForSensitiveActions = true;

    [ObservableProperty]
    private bool _useTokyoWhiteTheme;

    [ObservableProperty]
    private string _newPasswordStrengthLabel = "Very Weak";

    [ObservableProperty]
    private string _newPasswordStrengthColor = "#ff757f";

    [ObservableProperty]
    private int _newPasswordStrengthPercent = 20;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _showStatus;

    public ObservableCollection<CredentialViewModel> Credentials { get; } = [];
    public ObservableCollection<string> Categories { get; } = new(["all", .. Models.Categories.All]);
    public ObservableCollection<string> SortOptions { get; } = new(["Recent", "Site A-Z", "Weak First"]);
    public ObservableCollection<int> AutoLockOptions { get; } = new([1, 5, 10, 15, 30]);

    public int TotalCount => _vaultService.Count;
    public int FavoritesCount => Credentials.Count(c => c.IsFavorite);
    public int WeakCount => Credentials.Count(c => c.IsPasswordWeak);
    public int ReusedCount => Credentials.Count(c => c.IsPasswordReused);
    public bool IsEmpty => Credentials.Count == 0;
    public string FavoritesFilterLabel => FavoritesOnly ? "Favorites: On" : "Favorites Only";
    public string AutoLockSummary => $"Auto-lock after {AutoLockMinutes}m";

    public MainWindowViewModel()
    {
        var dataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");
        var vaultPath = Path.Combine(dataDirectory, "vault.dat");
        var uiStatePath = Path.Combine(dataDirectory, "ui-state.json");

        _vaultService = new VaultService(vaultPath);
        _uiStateService = new UiStateService(uiStatePath);

        ApplyUiState(_uiStateService.Load());
        UpdateNewPasswordStrength();
        StartSessionLoop();
    }

    partial void OnSearchTermChanged(string value)
    {
        RegisterActivity();
        if (IsAuthenticated)
        {
            LoadCredentials();
        }
    }

    partial void OnSelectedSortChanged(string value)
    {
        RegisterActivity();
        if (IsAuthenticated)
        {
            LoadCredentials();
        }

        PersistUiState();
    }

    partial void OnFavoritesOnlyChanged(bool value)
    {
        RegisterActivity();
        OnPropertyChanged(nameof(FavoritesFilterLabel));
        if (IsAuthenticated)
        {
            LoadCredentials();
        }

        PersistUiState();
    }

    partial void OnAutoLockMinutesChanged(int value)
    {
        RegisterActivity();
        OnPropertyChanged(nameof(AutoLockSummary));
        PersistUiState();
    }

    partial void OnUseTokyoWhiteThemeChanged(bool value)
    {
        RegisterActivity();
        PersistUiState();
    }

    partial void OnRequireReauthForSensitiveActionsChanged(bool value)
    {
        RegisterActivity();
        PersistUiState();
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        PersistUiState();
    }

    partial void OnNewPasswordChanged(string value)
    {
        RegisterActivity();
        UpdateNewPasswordStrength();
    }

    [RelayCommand]
    private void Login()
    {
        RegisterActivity();

        if (string.IsNullOrWhiteSpace(LoginPassword))
        {
            LoginError = "Password required";
            IsLoginFailed = true;
            return;
        }

        if (_vaultService.Initialize(LoginPassword))
        {
            _masterPassword = LoginPassword;
            IsAuthenticated = true;
            IsLoginFailed = false;
            LoginPassword = string.Empty;
            _lastReauthUtc = DateTimeOffset.UtcNow;
            LoadCredentials();
            ShowStatusMessage("Vault unlocked");
        }
        else
        {
            LoginError = "Invalid password";
            IsLoginFailed = true;
        }
    }

    [RelayCommand]
    private void Logout()
    {
        RegisterActivity();
        LockVault("Vault locked");
    }

    [RelayCommand]
    private void Search()
    {
        RegisterActivity();
        LoadCredentials();
    }

    [RelayCommand]
    private void ToggleFavoritesFilter()
    {
        FavoritesOnly = !FavoritesOnly;
    }

    [RelayCommand]
    private void SelectCategory(string? category)
    {
        RegisterActivity();
        SelectedCategory = category ?? "all";
        LoadCredentials();
    }

    [RelayCommand]
    private void ShowAddCredential()
    {
        RegisterActivity();

        NewSite = string.Empty;
        NewUsername = string.Empty;
        NewPassword = string.Empty;
        NewUrl = string.Empty;
        NewNotes = string.Empty;
        NewTotpSecret = string.Empty;
        NewCategory = SelectedCategory is not "all" and not "favorites" ? SelectedCategory : "general";
        NewIsFavorite = false;
        ShowAddDialog = true;
        UpdateNewPasswordStrength();
    }

    [RelayCommand]
    private void AddCredential()
    {
        RegisterActivity();

        if (string.IsNullOrWhiteSpace(NewSite) || string.IsNullOrWhiteSpace(NewPassword))
        {
            ShowStatusMessage("Site and password required");
            return;
        }

        var credential = new Credential
        {
            Site = NewSite,
            Username = NewUsername,
            Password = NewPassword,
            Url = NewUrl,
            Notes = NewNotes,
            Category = NewCategory,
            TotpSecret = NewTotpSecret,
            IsFavorite = NewIsFavorite
        };

        _vaultService.AddCredential(credential);
        _vaultService.SaveVault(_masterPassword);

        ShowAddDialog = false;
        LoadCredentials();
        ShowStatusMessage("Credential added");
    }

    [RelayCommand]
    private void EditCredential(CredentialViewModel? credential)
    {
        RegisterActivity();

        if (credential == null)
        {
            return;
        }

        NewSite = credential.Site;
        NewUsername = credential.Username;
        NewPassword = credential.Password;
        NewUrl = credential.Url;
        NewNotes = credential.Notes;
        NewCategory = credential.Category;
        NewTotpSecret = credential.TotpSecret;
        NewIsFavorite = credential.IsFavorite;

        SelectedCredential = credential;
        ShowEditDialog = true;
        UpdateNewPasswordStrength();
    }

    [RelayCommand]
    private void SaveEditCredential()
    {
        RegisterActivity();

        if (SelectedCredential == null)
        {
            return;
        }

        var credential = _vaultService.GetById(SelectedCredential.Id);
        if (credential == null)
        {
            return;
        }

        credential.Site = NewSite;
        credential.Username = NewUsername;
        credential.Password = NewPassword;
        credential.Url = NewUrl;
        credential.Notes = NewNotes;
        credential.Category = NewCategory;
        credential.TotpSecret = NewTotpSecret;
        credential.IsFavorite = NewIsFavorite;

        _vaultService.UpdateCredential(credential);
        _vaultService.SaveVault(_masterPassword);

        ShowEditDialog = false;
        LoadCredentials();
        ShowStatusMessage("Credential updated");
    }

    [RelayCommand]
    private void DeleteCredential(CredentialViewModel? credential)
    {
        RegisterActivity();

        if (credential == null)
        {
            return;
        }

        if (_vaultService.DeleteCredential(credential.Id))
        {
            _vaultService.SaveVault(_masterPassword);
            if (SelectedCredential?.Id == credential.Id)
            {
                ShowDetailPanel = false;
                SelectedCredential = null;
            }

            LoadCredentials();
            ShowStatusMessage("Credential deleted");
        }
    }

    [RelayCommand]
    private void ToggleFavorite(CredentialViewModel? credential)
    {
        RegisterActivity();

        if (credential == null)
        {
            return;
        }

        var wasFavorite = credential.IsFavorite;
        _vaultService.ToggleFavorite(credential.Id);
        _vaultService.SaveVault(_masterPassword);

        LoadCredentials();
        ShowStatusMessage(wasFavorite ? "Removed from favorites" : "Added to favorites");
    }

    [RelayCommand]
    private void SelectCredential(CredentialViewModel? credential)
    {
        RegisterActivity();
        SelectedCredential = credential;
        ShowDetailPanel = credential != null;
        ShowPasswordInDetail = false;
    }

    [RelayCommand]
    private void CopyPassword(CredentialViewModel? credential)
    {
        if (credential == null)
        {
            return;
        }

        ExecuteSensitiveAction(() =>
        {
            CopyToClipboard(credential.Password);
            ShowStatusMessage("Password copied (20s auto-clear)");
            StartClipboardAutoClear();
        });
    }

    [RelayCommand]
    private void CopyTotp(CredentialViewModel? credential)
    {
        if (credential == null || !credential.HasTotp)
        {
            ShowStatusMessage("No TOTP configured");
            return;
        }

        ExecuteSensitiveAction(() =>
        {
            if (!TotpService.TryGenerateCode(credential.TotpSecret, DateTimeOffset.UtcNow, out var code))
            {
                ShowStatusMessage("Invalid TOTP secret");
                return;
            }

            CopyToClipboard(code);
            ShowStatusMessage("2FA code copied");
            StartClipboardAutoClear();
        });
    }

    [RelayCommand]
    private void CopyUsername(CredentialViewModel? credential)
    {
        RegisterActivity();

        if (credential == null)
        {
            return;
        }

        CopyToClipboard(credential.Username);
        ShowStatusMessage("Username copied");
    }

    [RelayCommand]
    private void CopyUrl(CredentialViewModel? credential)
    {
        RegisterActivity();

        if (credential == null || string.IsNullOrWhiteSpace(credential.Url))
        {
            return;
        }

        CopyToClipboard(credential.Url);
        ShowStatusMessage("URL copied");
    }

    [RelayCommand]
    private void GeneratePassword()
    {
        RegisterActivity();

        const string lowercase = "abcdefghijklmnopqrstuvwxyz";
        const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string numbers = "0123456789";
        const string symbols = "!@#$%^&*()_+-=[]{}|;:,.<>?";

        var chars = string.Empty;
        if (IncludeLowercase) chars += lowercase;
        if (IncludeUppercase) chars += uppercase;
        if (IncludeNumbers) chars += numbers;
        if (IncludeSymbols) chars += symbols;

        if (string.IsNullOrEmpty(chars))
        {
            chars = lowercase + numbers;
        }

        var randomBytes = RandomNumberGenerator.GetBytes(Math.Max(PasswordLength, 8));
        var password = new char[randomBytes.Length];
        for (var i = 0; i < randomBytes.Length; i++)
        {
            password[i] = chars[randomBytes[i] % chars.Length];
        }

        GeneratedPassword = new string(password);
    }

    [RelayCommand]
    private void UseGeneratedPassword()
    {
        RegisterActivity();

        if (!string.IsNullOrEmpty(GeneratedPassword))
        {
            NewPassword = GeneratedPassword;
            GeneratedPassword = string.Empty;
        }
    }

    [RelayCommand]
    private void CancelAdd()
    {
        RegisterActivity();
        ShowAddDialog = false;
        ShowEditDialog = false;
    }

    [RelayCommand]
    private void ConfirmReauth()
    {
        RegisterActivity();

        if (!VerifyMasterPassword(ReauthPassword))
        {
            ReauthError = "Invalid master password";
            return;
        }

        _lastReauthUtc = DateTimeOffset.UtcNow;
        ReauthPassword = string.Empty;
        ReauthError = string.Empty;
        ShowReauthDialog = false;

        var pendingAction = _pendingSensitiveAction;
        _pendingSensitiveAction = null;
        pendingAction?.Invoke();
    }

    [RelayCommand]
    private void CancelReauth()
    {
        RegisterActivity();
        _pendingSensitiveAction = null;
        ReauthPassword = string.Empty;
        ReauthError = string.Empty;
        ShowReauthDialog = false;
    }

    public void RegisterActivity()
    {
        _lastActivityUtc = DateTimeOffset.UtcNow;
    }

    private void LoadCredentials()
    {
        if (!IsAuthenticated)
        {
            return;
        }

        Credentials.Clear();
        var sortKey = GetSortKey(SelectedSort);
        var credentials = _vaultService.SearchCredentials(SearchTerm, SelectedCategory, FavoritesOnly, sortKey);
        var reusedPasswordIds = _vaultService.GetReusedPasswordIds();

        foreach (var credential in credentials)
        {
            Credentials.Add(new CredentialViewModel(credential, reusedPasswordIds.Contains(credential.Id)));
        }

        RefreshTotpValues();
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(FavoritesCount));
        OnPropertyChanged(nameof(WeakCount));
        OnPropertyChanged(nameof(ReusedCount));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void ExecuteSensitiveAction(Action action)
    {
        RegisterActivity();

        if (!RequireReauthForSensitiveActions || DateTimeOffset.UtcNow - _lastReauthUtc <= SensitiveActionReauthWindow)
        {
            action();
            return;
        }

        _pendingSensitiveAction = action;
        ReauthPassword = string.Empty;
        ReauthError = string.Empty;
        ShowReauthDialog = true;
    }

    private bool VerifyMasterPassword(string candidate)
    {
        var candidateBytes = Encoding.UTF8.GetBytes(candidate ?? string.Empty);
        var masterBytes = Encoding.UTF8.GetBytes(_masterPassword);

        try
        {
            return candidateBytes.Length == masterBytes.Length &&
                   CryptographicOperations.FixedTimeEquals(candidateBytes, masterBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(candidateBytes);
            CryptographicOperations.ZeroMemory(masterBytes);
        }
    }

    private void UpdateNewPasswordStrength()
    {
        var score = PasswordHealthService.CalculateScore(NewPassword);
        NewPasswordStrengthLabel = PasswordHealthService.GetLabel(score);
        NewPasswordStrengthColor = PasswordHealthService.GetColor(score);
        NewPasswordStrengthPercent = PasswordHealthService.GetPercent(score);
    }

    private static string GetSortKey(string sortLabel) => sortLabel switch
    {
        "Site A-Z" => "site",
        "Weak First" => "weak",
        _ => "recent"
    };

    private void ApplyUiState(UiState state)
    {
        _isApplyingUiState = true;

        try
        {
            var normalizedCategory = string.IsNullOrWhiteSpace(state.SelectedCategory)
                ? "all"
                : state.SelectedCategory.Trim().ToLowerInvariant();

            if (normalizedCategory == "favorites")
            {
                normalizedCategory = "all";
                FavoritesOnly = true;
            }

            SelectedCategory = Categories.Contains(normalizedCategory) ? normalizedCategory : "all";
            FavoritesOnly = state.FavoritesOnly;
            SelectedSort = SortOptions.Contains(state.SelectedSort) ? state.SelectedSort : "Recent";
            AutoLockMinutes = AutoLockOptions.Contains(state.AutoLockMinutes) ? state.AutoLockMinutes : 5;
            RequireReauthForSensitiveActions = state.RequireReauthForSensitiveActions;
            UseTokyoWhiteTheme = state.UseTokyoWhiteTheme;
        }
        finally
        {
            _isApplyingUiState = false;
        }
    }

    private void PersistUiState()
    {
        if (_isApplyingUiState)
        {
            return;
        }

        _uiStateService.Save(new UiState
        {
            SelectedCategory = SelectedCategory,
            FavoritesOnly = FavoritesOnly,
            SelectedSort = SelectedSort,
            AutoLockMinutes = AutoLockMinutes,
            RequireReauthForSensitiveActions = RequireReauthForSensitiveActions,
            UseTokyoWhiteTheme = UseTokyoWhiteTheme
        });
    }

    private void RefreshTotpValues()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var credential in Credentials)
        {
            credential.RefreshTotp(now);
        }
    }

    private void StartSessionLoop()
    {
        _sessionLoopCts?.Cancel();
        _sessionLoopCts = new CancellationTokenSource();
        var token = _sessionLoopCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!IsAuthenticated)
                    {
                        return;
                    }

                    RefreshTotpValues();

                    if (DateTimeOffset.UtcNow - _lastActivityUtc >= TimeSpan.FromMinutes(Math.Max(AutoLockMinutes, 1)))
                    {
                        LockVault("Vault auto-locked due to inactivity");
                    }
                });
            }
        }, token);
    }

    private void LockVault(string statusMessage)
    {
        _masterPassword = string.Empty;
        _pendingSensitiveAction = null;
        _lastReauthUtc = DateTimeOffset.MinValue;

        IsAuthenticated = false;
        IsLoginFailed = false;
        LoginPassword = string.Empty;
        ShowDetailPanel = false;
        ShowAddDialog = false;
        ShowEditDialog = false;
        ShowReauthDialog = false;
        SelectedCredential = null;
        Credentials.Clear();

        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(FavoritesCount));
        OnPropertyChanged(nameof(WeakCount));
        OnPropertyChanged(nameof(ReusedCount));
        OnPropertyChanged(nameof(IsEmpty));

        ShowStatusMessage(statusMessage);
    }

    private void ShowStatusMessage(string message)
    {
        StatusMessage = message;
        ShowStatus = true;

        var sequence = Interlocked.Increment(ref _statusSequence);
        Task.Run(async () =>
        {
            await Task.Delay(3000);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (sequence == _statusSequence)
                {
                    ShowStatus = false;
                }
            });
        });
    }

    private static void CopyToClipboard(string text)
    {
        Task.Run(() =>
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "xclip",
                        Arguments = "-selection clipboard",
                        RedirectStandardInput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.StandardInput.Write(text);
                process.StandardInput.Close();
                process.WaitForExit();
            }
            catch
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "xsel",
                        Arguments = "--clipboard --input",
                        RedirectStandardInput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.StandardInput.Write(text);
                process.StandardInput.Close();
                process.WaitForExit();
            }
        });
    }

    private static void StartClipboardAutoClear()
    {
        Task.Run(async () =>
        {
            await Task.Delay(20000);
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "xclip",
                        Arguments = "-selection clipboard -i /dev/null",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit();
            }
            catch
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "xsel",
                            Arguments = "--clipboard --clear",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    process.WaitForExit();
                }
                catch
                {
                }
            }
        });
    }
}

public partial class CredentialViewModel : ViewModelBase
{
    private readonly Credential _credential;

    [ObservableProperty]
    private string _currentTotpCode = string.Empty;

    [ObservableProperty]
    private int _totpSecondsRemaining;

    public string Id => _credential.Id;
    public string Site => _credential.Site;
    public string Username => _credential.Username;
    public string Password => _credential.Password;
    public string Url => _credential.Url;
    public string Notes => _credential.Notes;
    public string Category => _credential.Category;
    public string TotpSecret => _credential.TotpSecret;
    public bool HasTotp => TotpService.HasSecret(_credential.TotpSecret);
    public bool IsFavorite => _credential.IsFavorite;
    public string CategoryIcon => Models.Categories.GetIcon(_credential.Category);
    public string CategoryColor => Models.Categories.GetColor(_credential.Category);
    public string CreatedAt => _credential.CreatedAt.ToString("MMM dd, yyyy");
    public string ModifiedAt => _credential.ModifiedAt.ToString("MMM dd, yyyy");
    public int PasswordScore { get; }
    public string PasswordStrengthLabel { get; }
    public string PasswordStrengthColor { get; }
    public bool IsPasswordWeak => PasswordHealthService.IsWeak(_credential.Password);
    public bool IsPasswordReused { get; }
    public string PasswordHealthText => IsPasswordReused ? $"{PasswordStrengthLabel} • Reused" : PasswordStrengthLabel;

    public CredentialViewModel(Credential credential, bool isPasswordReused)
    {
        _credential = credential;
        IsPasswordReused = isPasswordReused;

        PasswordScore = PasswordHealthService.CalculateScore(_credential.Password);
        PasswordStrengthLabel = PasswordHealthService.GetLabel(PasswordScore);
        PasswordStrengthColor = PasswordHealthService.GetColor(PasswordScore);

        RefreshTotp(DateTimeOffset.UtcNow);
    }

    public void RefreshTotp(DateTimeOffset now)
    {
        if (!HasTotp)
        {
            CurrentTotpCode = string.Empty;
            TotpSecondsRemaining = 0;
            return;
        }

        if (TotpService.TryGenerateCode(_credential.TotpSecret, now, out var code))
        {
            CurrentTotpCode = code;
            TotpSecondsRemaining = TotpService.GetSecondsRemaining(now);
        }
        else
        {
            CurrentTotpCode = "Invalid Secret";
            TotpSecondsRemaining = 0;
        }
    }
}
