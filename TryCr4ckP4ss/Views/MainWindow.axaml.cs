using Avalonia.Controls;
using Avalonia.Input;
using TryCr4ckP4ss.ViewModels;

namespace TryCr4ckP4ss.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainWindowViewModel vm)
        {
            vm.SearchCommand.Execute(null);
        }
    }

    private void OnLoginKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainWindowViewModel vm)
        {
            vm.LoginCommand.Execute(null);
        }
    }

    private void OnCredentialClick(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is CredentialViewModel cred && DataContext is MainWindowViewModel vm)
        {
            vm.EditCredentialCommand.Execute(cred);
        }
    }
}
