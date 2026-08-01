using Microsoft.Win32;

namespace SteamAccountSwitcher.Services;

public sealed class SteamLocator
{
    public string? FindSteamExecutable(string? configuredPath = null)
    {
        if (IsSteamExecutable(configuredPath))
        {
            return Path.GetFullPath(configuredPath!);
        }

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view);
            using var key = baseKey.OpenSubKey(@"Software\Valve\Steam");
            var executable = key?.GetValue("SteamExe") as string;
            if (IsSteamExecutable(executable))
            {
                return Path.GetFullPath(executable!);
            }

            var installPath = key?.GetValue("SteamPath") as string;
            var candidate = installPath is null ? null : Path.Combine(installPath, "steam.exe");
            if (IsSteamExecutable(candidate))
            {
                return Path.GetFullPath(candidate!);
            }
        }

        var commonPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steam.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steam.exe")
        };
        return commonPaths.FirstOrDefault(IsSteamExecutable);
    }

    public string GetLoginUsersPath(string steamExecutable) =>
        Path.Combine(Path.GetDirectoryName(steamExecutable)
            ?? throw new InvalidOperationException("Steam has no installation directory."), "config", "loginusers.vdf");

    private static bool IsSteamExecutable(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && File.Exists(path)
        && string.Equals(Path.GetFileName(path), "steam.exe", StringComparison.OrdinalIgnoreCase);
}
