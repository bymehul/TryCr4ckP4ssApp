using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using TryCr4ckP4ss.ViewModels;

namespace TryCr4ckP4ss.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _boundVm;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        OnDataContextChanged(this, EventArgs.Empty);
    }

    private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_boundVm != null)
        {
            _boundVm.PropertyChanged -= OnVmPropertyChanged;
        }

        _boundVm = DataContext as MainWindowViewModel;
        if (_boundVm != null)
        {
            _boundVm.PropertyChanged += OnVmPropertyChanged;
            ApplyThemePalette(_boundVm.UseTokyoWhiteTheme);
            return;
        }

        ApplyThemePalette(false);
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainWindowViewModel vm || e.PropertyName != nameof(MainWindowViewModel.UseTokyoWhiteTheme))
        {
            return;
        }

        ApplyThemePalette(vm.UseTokyoWhiteTheme);
    }

    private void ApplyThemePalette(bool useTokyoWhiteTheme)
    {
        RequestedThemeVariant = useTokyoWhiteTheme ? ThemeVariant.Light : ThemeVariant.Dark;

        if (useTokyoWhiteTheme)
        {
            SetBrush("WindowBackgroundBrush", "#f4f7ff");
            SetBrush("SidebarBackgroundBrush", "#e8eefb");
            SetBrush("InputBackgroundBrush", "#ffffff");
            SetBrush("CardBackgroundBrush", "#ffffff");
            SetBrush("InsetBackgroundBrush", "#edf2fb");
            SetBrush("ButtonBackgroundBrush", "#e4ebfa");
            SetBrush("ButtonHoverBrush", "#d8e2f8");
            SetBrush("ButtonPressedBrush", "#ccd8f2");
            SetBrush("SecurityCardBrush", "#edf2fd");
            SetBrush("TextPrimaryBrush", "#1f2942");
            SetBrush("TextMutedBrush", "#3d4b6e");
            SetBrush("TextSubtleBrush", "#6b7a9c");
            SetBrush("BorderBrushColor", "#c4d0e9");
            SetBrush("AccentBrush", "#4f7fd9");
            SetBrush("AccentHoverBrush", "#6994e8");
            SetBrush("AccentForegroundBrush", "#ffffff");
            SetBrush("DangerBrush", "#d64f61");
            SetBrush("DangerForegroundBrush", "#ffffff");
            SetBrush("AddTitleBrush", "#7a5dc6");
            SetBrush("EditTitleBrush", "#2e7abf");
            SetBrush("TotpBrush", "#2e7abf");
            SetBrush("OverlayBrush", "#b3dfe6f8");
            SetBrush("OverlaySoftBrush", "#99d3dcef");
            return;
        }

        SetBrush("WindowBackgroundBrush", "#1a1b26");
        SetBrush("SidebarBackgroundBrush", "#16161e");
        SetBrush("InputBackgroundBrush", "#24283b");
        SetBrush("CardBackgroundBrush", "#24283b");
        SetBrush("InsetBackgroundBrush", "#1a1b26");
        SetBrush("ButtonBackgroundBrush", "#292e42");
        SetBrush("ButtonHoverBrush", "#3b4261");
        SetBrush("ButtonPressedBrush", "#414868");
        SetBrush("SecurityCardBrush", "#1f2335");
        SetBrush("TextPrimaryBrush", "#c0caf5");
        SetBrush("TextMutedBrush", "#a9b1d6");
        SetBrush("TextSubtleBrush", "#565f89");
        SetBrush("BorderBrushColor", "#414868");
        SetBrush("AccentBrush", "#7aa2f7");
        SetBrush("AccentHoverBrush", "#9bb8f8");
        SetBrush("AccentForegroundBrush", "#1a1b26");
        SetBrush("DangerBrush", "#ff757f");
        SetBrush("DangerForegroundBrush", "#1a1b26");
        SetBrush("AddTitleBrush", "#bb9af7");
        SetBrush("EditTitleBrush", "#7dcfff");
        SetBrush("TotpBrush", "#7dcfff");
        SetBrush("OverlayBrush", "#c01a1b26");
        SetBrush("OverlaySoftBrush", "#b01a1b26");
    }

    private void SetBrush(string key, string hex)
    {
        Resources[key] = new SolidColorBrush(Color.Parse(hex));
    }

    private void OnUserActivity(object? sender, PointerPressedEventArgs e)
    {
        Vm?.RegisterActivity();
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (Vm == null)
        {
            return;
        }

        Vm.RegisterActivity();

        var hasCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (!hasCtrl)
        {
            return;
        }

        if (e.Key == Key.N)
        {
            Vm.ShowAddCredentialCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F)
        {
            if (this.FindControl<TextBox>("SearchBox") is { } searchBox)
            {
                searchBox.Focus();
                searchBox.SelectAll();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.L)
        {
            Vm.LogoutCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        Vm?.RegisterActivity();

        if (e.Key == Key.Enter && Vm is { } vm)
        {
            vm.SearchCommand.Execute(null);
        }
    }

    private void OnLoginKeyDown(object? sender, KeyEventArgs e)
    {
        Vm?.RegisterActivity();

        if (e.Key == Key.Enter && Vm is { } vm)
        {
            vm.LoginCommand.Execute(null);
        }
    }

    private void OnReauthKeyDown(object? sender, KeyEventArgs e)
    {
        Vm?.RegisterActivity();

        if (e.Key == Key.Enter && Vm is { } vm)
        {
            vm.ConfirmReauthCommand.Execute(null);
        }
    }

    private void OnCredentialClick(object? sender, PointerPressedEventArgs e)
    {
        Vm?.RegisterActivity();

        if (sender is Border border && border.DataContext is CredentialViewModel cred && Vm is { } vm)
        {
            vm.EditCredentialCommand.Execute(cred);
        }
    }
}
