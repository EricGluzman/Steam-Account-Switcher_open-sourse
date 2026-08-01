using System.Runtime.InteropServices;

namespace SteamAccountSwitcher.Services;

public sealed class DesktopCopyService
{
    public string DesktopCopyPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        "SteamAccountSwitcher.exe");

    public async Task CreateAsync(bool overwrite, CancellationToken cancellationToken = default)
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException("The currently running executable could not be located.");
        }

        await CopyExecutableAsync(processPath, DesktopCopyPath, overwrite, cancellationToken);
        File.SetAttributes(DesktopCopyPath, FileAttributes.Normal);
        NotifyDesktopCreated(DesktopCopyPath);
    }

    private static void NotifyDesktopCreated(string path)
    {
        const uint shcneCreate = 0x00000002;
        const uint shcneUpdateDir = 0x00001000;
        const uint shcnfPathW = 0x0005;
        SHChangeNotify(shcneCreate, shcnfPathW, path, IntPtr.Zero);
        SHChangeNotify(
            shcneUpdateDir,
            shcnfPathW,
            Path.GetDirectoryName(path) ?? path,
            IntPtr.Zero);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(
        uint eventId,
        uint flags,
        string item1,
        IntPtr item2);

    public static async Task CopyExecutableAsync(
        string sourcePath,
        string destinationPath,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        var sourceFullPath = Path.GetFullPath(sourcePath);
        var destinationFullPath = Path.GetFullPath(destinationPath);
        if (string.Equals(sourceFullPath, destinationFullPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!File.Exists(sourceFullPath))
        {
            throw new FileNotFoundException("The currently running executable could not be found.", sourceFullPath);
        }
        if (File.Exists(destinationFullPath) && !overwrite)
        {
            throw new IOException("A desktop copy already exists.");
        }

        var destinationDirectory = Path.GetDirectoryName(destinationFullPath)
            ?? throw new InvalidOperationException("The Desktop folder could not be located.");
        Directory.CreateDirectory(destinationDirectory);

        var temporaryPath = Path.Combine(
            destinationDirectory,
            $".SteamAccountSwitcher.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var source = new FileStream(
                sourceFullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var destination = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await source.CopyToAsync(destination, cancellationToken);
            }

            File.Move(temporaryPath, destinationFullPath, overwrite);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
