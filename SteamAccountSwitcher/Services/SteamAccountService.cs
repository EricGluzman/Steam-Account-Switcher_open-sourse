using System.Diagnostics;
using Microsoft.Win32;
using SteamAccountSwitcher.Models;

namespace SteamAccountSwitcher.Services;

public sealed class SteamAccountService
{
    private readonly SteamLocator _locator;
    private readonly SteamVdfService _vdf;

    public SteamAccountService(SteamLocator locator, SteamVdfService vdf)
    {
        _locator = locator;
        _vdf = vdf;
    }

    public string ResolveSteamPath(string? configuredPath) =>
        _locator.FindSteamExecutable(configuredPath)
        ?? throw new FileNotFoundException(
            "Steam.exe was not found. Open Settings and select your Steam executable.");

    public IReadOnlyList<RememberedSteamAccount> GetRememberedAccounts(string? configuredPath)
    {
        var steamPath = ResolveSteamPath(configuredPath);
        return _vdf.ReadAccounts(_locator.GetLoginUsersPath(steamPath));
    }

    public async Task SwitchAsync(
        SteamAccount account,
        string? configuredPath,
        bool showAccountChooser,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var steamPath = ResolveSteamPath(configuredPath);
        var loginUsersPath = _locator.GetLoginUsersPath(steamPath);
        var configPath = Path.Combine(
            Path.GetDirectoryName(loginUsersPath)
                ?? throw new InvalidOperationException("Steam's config directory was not found."),
            "config.vdf");
        string? password = null;
        var vdfChanged = false;
        var configChanged = false;
        var registryChanged = false;
        object? previousAutoLoginUser = null;
        object? previousRememberPassword = null;

        if (account.LoginMode == LoginMode.RememberedSession)
        {
            if (string.IsNullOrWhiteSpace(account.SteamId64))
            {
                throw new InvalidOperationException("This card is not linked to a remembered Steam account.");
            }
            if (!_vdf.ReadAccounts(loginUsersPath).Any(item => item.SteamId64 == account.SteamId64))
            {
                throw new InvalidOperationException("That account is no longer remembered by Steam.");
            }
            using var currentKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            previousAutoLoginUser = currentKey?.GetValue("AutoLoginUser");
            previousRememberPassword = currentKey?.GetValue("RememberPassword");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(account.EncryptedPassword))
            {
                throw new InvalidOperationException("This account does not have a saved encrypted password.");
            }
            password = DpapiService.Unprotect(account.EncryptedPassword);
        }

        progress?.Report("Closing Steam safely…");
        try
        {
            await ShutdownSteamAsync(steamPath, cancellationToken);
            configChanged = _vdf.SetUserChooser(configPath, showAccountChooser);

            if (account.LoginMode == LoginMode.RememberedSession)
            {
                progress?.Report("Selecting remembered account…");
                _vdf.ActivateAccount(loginUsersPath, account.SteamId64!);
                vdfChanged = true;
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Valve\Steam", true)
                    ?? throw new InvalidOperationException("Steam's Windows registry settings could not be opened.");
                registryChanged = true;
                key.SetValue("AutoLoginUser", account.Username, RegistryValueKind.String);
                key.SetValue("RememberPassword", 1, RegistryValueKind.DWord);
                StartSteam(steamPath);
            }
            else
            {
                progress?.Report("Starting Steam with protected credentials…");
                StartSteam(steamPath, account.Username, password!);
            }

            progress?.Report($"Steam is starting as {account.DisplayName}.");
        }
        catch
        {
            var backupPath = loginUsersPath + ".switcher.bak";
            if (vdfChanged && File.Exists(backupPath))
            {
                File.Copy(backupPath, loginUsersPath, true);
            }
            var configBackupPath = configPath + ".switcher.bak";
            if (configChanged && File.Exists(configBackupPath))
            {
                File.Copy(configBackupPath, configPath, true);
            }
            if (registryChanged)
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Valve\Steam", true);
                RestoreRegistryValue(key, "AutoLoginUser", previousAutoLoginUser, RegistryValueKind.String);
                RestoreRegistryValue(key, "RememberPassword", previousRememberPassword, RegistryValueKind.DWord);
            }
            try
            {
                if (!IsSteamRunning())
                {
                    StartSteam(steamPath);
                }
            }
            catch
            {
                // Preserve the original switching failure.
            }
            throw;
        }
        finally
        {
            password = null;
        }
    }

    private static void RestoreRegistryValue(
        RegistryKey? key,
        string name,
        object? value,
        RegistryValueKind kind)
    {
        if (key is null)
        {
            return;
        }
        if (value is null)
        {
            key.DeleteValue(name, false);
        }
        else
        {
            key.SetValue(name, value, kind);
        }
    }

    private static async Task ShutdownSteamAsync(string steamPath, CancellationToken cancellationToken)
    {
        if (!IsSteamRunning())
        {
            return;
        }

        Process.Start(new ProcessStartInfo(steamPath)
        {
            UseShellExecute = false,
            Arguments = "-shutdown",
            CreateNoWindow = true
        })?.Dispose();

        var timeoutAt = DateTime.UtcNow.AddSeconds(25);
        while (DateTime.UtcNow < timeoutAt)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsSteamRunning())
            {
                return;
            }
            await Task.Delay(350, cancellationToken);
        }

        throw new TimeoutException(
            "Steam did not close within 25 seconds. Close it manually and try again; no files were changed.");
    }

    private static bool IsSteamRunning()
    {
        var processes = Process.GetProcessesByName("steam");
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static void StartSteam(string steamPath, string? username = null, string? password = null)
    {
        var startInfo = new ProcessStartInfo(steamPath)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(steamPath) ?? Environment.CurrentDirectory
        };
        if (username is not null && password is not null)
        {
            startInfo.ArgumentList.Add("-login");
            startInfo.ArgumentList.Add(username);
            startInfo.ArgumentList.Add(password);
        }
        Process.Start(startInfo)?.Dispose();
    }
}
