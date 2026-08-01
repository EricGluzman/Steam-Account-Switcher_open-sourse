using System.Text.Json;
using SteamAccountSwitcher.Models;

namespace SteamAccountSwitcher.Services;

public sealed class AccountStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string DataDirectory { get; }
    public string DataFilePath => Path.Combine(DataDirectory, "accounts.json");

    public AccountStore(string? dataDirectory = null)
    {
        DataDirectory = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SteamAccountSwitcher");
    }

    public async Task<AppDataDocument> LoadAsync()
    {
        if (!File.Exists(DataFilePath))
        {
            return new AppDataDocument();
        }

        try
        {
            await using var stream = File.OpenRead(DataFilePath);
            var document = await JsonSerializer.DeserializeAsync<AppDataDocument>(stream, JsonOptions)
                ?? new AppDataDocument();
            document.Accounts ??= [];
            document.Settings ??= new AppSettings();
            return document;
        }
        catch (JsonException exception)
        {
            var recoveryPath = Path.Combine(
                DataDirectory,
                $"accounts.corrupt-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
            File.Move(DataFilePath, recoveryPath, true);
            throw new InvalidDataException(
                $"Saved account data was damaged and moved to {recoveryPath}. No credentials were overwritten.",
                exception);
        }
    }

    public async Task SaveAsync(AppDataDocument document)
    {
        Directory.CreateDirectory(DataDirectory);
        var temporaryPath = DataFilePath + ".tmp";
        await using (var stream = new FileStream(
            temporaryPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            4096,
            FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, document, JsonOptions);
            await stream.FlushAsync();
        }

        File.Move(temporaryPath, DataFilePath, true);
    }
}
