using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TryCr4ckP4ss.Models;
using TryCr4ckP4ss.Services;

namespace TryCr4ckP4ss.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly VaultService _vaultService;
    private string _masterPassword = string.Empty;

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
    private CredentialViewModel? _selectedCredential;

    [ObservableProperty]
    private bool _showAddDialog;

    [ObservableProperty]
    private bool _showEditDialog;

    [ObservableProperty]
    private bool _showDetailPanel;

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
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _showStatus;

    public ObservableCollection<CredentialViewModel> Credentials { get; } = [];
    public ObservableCollection<string> Categories { get; } = new(["all", ..Models.Categories.All]);
    
    public int TotalCount => _vaultService.Count;
    public int FavoritesCount => Credentials.Count(c => c.IsFavorite);
    public bool IsEmpty => Credentials.Count == 0;

    public MainWindowViewModel()
    {
        var vaultPath = Path.Combine(AppContext.BaseDirectory, "Data", "vault.dat");
        _vaultService = new VaultService(vaultPath);
    }

    [RelayCommand]
    private void Login()
    {
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
        _masterPassword = string.Empty;
        IsAuthenticated = false;
        ShowDetailPanel = false;
        Credentials.Clear();
        ShowStatusMessage("Vault locked");
    }

    private void LoadCredentials()
    {
        Credentials.Clear();
        var credentials = string.IsNullOrWhiteSpace(SearchTerm) && SelectedCategory == "all"
            ? _vaultService.GetAllCredentials()
            : _vaultService.SearchCredentials(SearchTerm, SelectedCategory);

        foreach (var cred in credentials)
        {
            Credentials.Add(new CredentialViewModel(cred));
        }
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(FavoritesCount));
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private void Search()
    {
        LoadCredentials();
    }

    [RelayCommand]
    private void SelectCategory(string? category)
    {
        SelectedCategory = category ?? "all";
        LoadCredentials();
    }

    [RelayCommand]
    private void ShowAddCredential()
    {
        NewSite = string.Empty;
        NewUsername = string.Empty;
        NewPassword = string.Empty;
        NewUrl = string.Empty;
        NewNotes = string.Empty;
        NewCategory = "general";
        NewIsFavorite = false;
        ShowAddDialog = true;
    }

    [RelayCommand]
    private void AddCredential()
    {
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
            IsFavorite = NewIsFavorite
        };

        _vaultService.AddCredential(credential);
        _vaultService.SaveVault(_masterPassword);
        
        Credentials.Insert(0, new CredentialViewModel(credential));
        OnPropertyChanged(nameof(TotalCount));
        ShowAddDialog = false;
        ShowStatusMessage("Credential added");
    }

    [RelayCommand]
    private void EditCredential(CredentialViewModel? credential)
    {
        if (credential == null) return;

        NewSite = credential.Site;
        NewUsername = credential.Username;
        NewPassword = credential.Password;
        NewUrl = credential.Url;
        NewNotes = credential.Notes;
        NewCategory = credential.Category;
        NewIsFavorite = credential.IsFavorite;
        
        SelectedCredential = credential;
        ShowEditDialog = true;
    }

    [RelayCommand]
    private void SaveEditCredential()
    {
        if (SelectedCredential == null) return;

        var credential = _vaultService.GetById(SelectedCredential.Id);
        if (credential == null) return;

        credential.Site = NewSite;
        credential.Username = NewUsername;
        credential.Password = NewPassword;
        credential.Url = NewUrl;
        credential.Notes = NewNotes;
        credential.Category = NewCategory;
        credential.IsFavorite = NewIsFavorite;

        _vaultService.UpdateCredential(credential);
        _vaultService.SaveVault(_masterPassword);

        var vm = Credentials.FirstOrDefault(c => c.Id == credential.Id);
        if (vm != null)
        {
            var index = Credentials.IndexOf(vm);
            Credentials[index] = new CredentialViewModel(credential);
        }

        ShowEditDialog = false;
        ShowStatusMessage("Credential updated");
    }

    [RelayCommand]
    private void DeleteCredential(CredentialViewModel? credential)
    {
        if (credential == null) return;

        if (_vaultService.DeleteCredential(credential.Id))
        {
            _vaultService.SaveVault(_masterPassword);
            Credentials.Remove(credential);
            if (SelectedCredential?.Id == credential.Id)
            {
                ShowDetailPanel = false;
            }
            OnPropertyChanged(nameof(TotalCount));
            ShowStatusMessage("Credential deleted");
        }
    }

    [RelayCommand]
    private void ToggleFavorite(CredentialViewModel? credential)
    {
        if (credential == null) return;

        _vaultService.ToggleFavorite(credential.Id);
        _vaultService.SaveVault(_masterPassword);

        var updated = _vaultService.GetById(credential.Id);
        if (updated != null)
        {
            var index = Credentials.IndexOf(credential);
            if (index >= 0)
            {
                Credentials[index] = new CredentialViewModel(updated);
            }
        }
        
        OnPropertyChanged(nameof(FavoritesCount));
        ShowStatusMessage(credential.IsFavorite ? "Removed from favorites" : "Added to favorites");
    }

    [RelayCommand]
    private void SelectCredential(CredentialViewModel? credential)
    {
        SelectedCredential = credential;
        ShowDetailPanel = credential != null;
        ShowPasswordInDetail = false;
    }

    [RelayCommand]
    private void CopyPassword(CredentialViewModel? credential)
    {
        if (credential == null) return;
        CopyToClipboard(credential.Password);
        ShowStatusMessage("Password copied (20s auto-clear)");
        StartClipboardAutoClear();
    }

    [RelayCommand]
    private void CopyUsername(CredentialViewModel? credential)
    {
        if (credential == null) return;
        CopyToClipboard(credential.Username);
        ShowStatusMessage("Username copied");
    }

    [RelayCommand]
    private void CopyUrl(CredentialViewModel? credential)
    {
        if (credential == null || string.IsNullOrWhiteSpace(credential.Url)) return;
        CopyToClipboard(credential.Url);
        ShowStatusMessage("URL copied");
    }

    [RelayCommand]
    private void GeneratePassword()
    {
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

        var random = new Random();
        var password = new char[PasswordLength];
        for (int i = 0; i < PasswordLength; i++)
        {
            password[i] = chars[random.Next(chars.Length)];
        }

        GeneratedPassword = new string(password);
    }

    [RelayCommand]
    private void UseGeneratedPassword()
    {
        if (!string.IsNullOrEmpty(GeneratedPassword))
        {
            NewPassword = GeneratedPassword;
            GeneratedPassword = string.Empty;
        }
    }

    [RelayCommand]
    private void CancelAdd()
    {
        ShowAddDialog = false;
        ShowEditDialog = false;
    }

    private void ShowStatusMessage(string message)
    {
        StatusMessage = message;
        ShowStatus = true;
        
        Task.Run(async () =>
        {
            await Task.Delay(3000);
            ShowStatus = false;
        });
    }

    private void CopyToClipboard(string text)
    {
        Task.Run(() =>
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
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
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
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

    private void StartClipboardAutoClear()
    {
        Task.Run(async () =>
        {
            await Task.Delay(20000);
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "xclip",
                        Arguments = "-selection clipboard /dev/null",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                process.WaitForExit();
            }
            catch { }
        });
    }
}

public partial class CredentialViewModel : ViewModelBase
{
    private readonly Credential _credential;

    public string Id => _credential.Id;
    public string Site => _credential.Site;
    public string Username => _credential.Username;
    public string Password => _credential.Password;
    public string Url => _credential.Url;
    public string Notes => _credential.Notes;
    public string Category => _credential.Category;
    public bool IsFavorite => _credential.IsFavorite;
    public string CategoryIcon => Models.Categories.GetIcon(_credential.Category);
    public string CategoryColor => Models.Categories.GetColor(_credential.Category);
    public string CreatedAt => _credential.CreatedAt.ToString("MMM dd, yyyy");
    public string ModifiedAt => _credential.ModifiedAt.ToString("MMM dd, yyyy");

    public CredentialViewModel(Credential credential)
    {
        _credential = credential;
    }
}
