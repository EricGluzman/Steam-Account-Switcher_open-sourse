using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SteamAccountSwitcher.Models;
using SteamAccountSwitcher.Services;
using SteamAccountSwitcher.ViewModels;
using SteamAccountSwitcher.Views;

namespace SteamAccountSwitcher;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        var locator = new SteamLocator();
        var steamService = new SteamAccountService(locator, new SteamVdfService());
        _viewModel = new MainViewModel(
            new AccountStore(),
            steamService,
            new SteamAvatarService());
        DataContext = _viewModel;
        Loaded += async (_, _) => await RunUiActionAsync(_viewModel.InitializeAsync);
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsBusy) return;
        IReadOnlyList<RememberedSteamAccount> remembered = [];
        try
        {
            remembered = _viewModel.GetRememberedAccounts();
        }
        catch (Exception exception)
        {
            var continueResult = MessageBox.Show(
                $"{exception.Message}\n\nYou can still add an encrypted-password account. Continue?",
                "Steam not detected",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
            if (continueResult != MessageBoxResult.Yes)
            {
                return;
            }
        }

        var dialog = new AccountDialog(remembered) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Result is not null)
        {
            await RunUiActionAsync(() => _viewModel.AddAsync(dialog.Result));
        }
    }

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (_viewModel.IsBusy) return;
        if (sender is not Button { Tag: SteamAccount account })
        {
            return;
        }

        IReadOnlyList<RememberedSteamAccount> remembered = [];
        if (account.LoginMode == LoginMode.RememberedSession)
        {
            try
            {
                remembered = _viewModel.GetRememberedAccounts();
            }
            catch (Exception exception)
            {
                ShowError(exception);
                return;
            }
        }

        var dialog = new AccountDialog(remembered, account) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Result is not null)
        {
            await RunUiActionAsync(() => _viewModel.UpdateAsync(dialog.Result));
        }
    }

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (_viewModel.IsBusy) return;
        if (sender is not Button { Tag: SteamAccount account })
        {
            return;
        }
        var answer = MessageBox.Show(
            $"Remove “{account.DisplayName}” from this app?\n\nSteam's own remembered login will not be changed.",
            "Remove account",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes)
        {
            await RunUiActionAsync(() => _viewModel.RemoveAsync(account));
        }
    }

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsBusy) return;
        var dialog = new SettingsDialog(_viewModel.Settings) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Result is not null)
        {
            await RunUiActionAsync(() => _viewModel.SaveSettingsAsync(dialog.Result));
        }
    }

    private async void AccountCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.IsBusy || FindVisualParent<Button>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }
        if (sender is not Border { DataContext: SteamAccount account })
        {
            return;
        }
        if (_viewModel.Settings.ConfirmBeforeSwitch)
        {
            var answer = MessageBox.Show(
                $"Close Steam and switch to “{account.DisplayName}”?",
                "Switch Steam account",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }
        }

        await RunUiActionAsync(() => _viewModel.SwitchAsync(account));
    }

    private async Task RunUiActionAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void ShowError(Exception exception) =>
        MessageBox.Show(exception.Message, "Steam Account Switcher", MessageBoxButton.OK, MessageBoxImage.Error);

    private static T? FindVisualParent<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T result)
            {
                return result;
            }
            source = VisualTreeHelper.GetParent(source);
        }
        return null;
    }
}
