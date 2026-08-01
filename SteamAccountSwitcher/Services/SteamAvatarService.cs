using System.Net.Http;
using System.Xml.Linq;

namespace SteamAccountSwitcher.Services;

public sealed class SteamAvatarService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    private readonly string _cacheDirectory;

    public SteamAvatarService(string? cacheDirectory = null)
    {
        _cacheDirectory = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SteamAccountSwitcher",
            "avatars");
    }

    public async Task<string?> GetAvatarPathAsync(
        string? steamId64,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(steamId64) || !steamId64.All(char.IsAsciiDigit))
        {
            return null;
        }

        Directory.CreateDirectory(_cacheDirectory);
        var cachedPath = Path.Combine(_cacheDirectory, $"{steamId64}.jpg");
        if (File.Exists(cachedPath) && new FileInfo(cachedPath).Length > 0)
        {
            return cachedPath;
        }

        try
        {
            var profileUrl = $"https://steamcommunity.com/profiles/{steamId64}?xml=1";
            var xml = await Http.GetStringAsync(profileUrl, cancellationToken);
            var avatarValue = XDocument.Parse(xml)
                .Descendants("avatarMedium")
                .Select(element => element.Value.Trim())
                .FirstOrDefault();
            if (!Uri.TryCreate(avatarValue, UriKind.Absolute, out var avatarUri)
                || avatarUri.Scheme != Uri.UriSchemeHttps
                || !IsTrustedSteamImageHost(avatarUri.Host))
            {
                return null;
            }

            using var response = await Http.GetAsync(
                avatarUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentType?.MediaType?.StartsWith(
                    "image/",
                    StringComparison.OrdinalIgnoreCase) != true
                || response.Content.Headers.ContentLength > 2_000_000)
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (bytes.Length is 0 or > 2_000_000)
            {
                return null;
            }

            var temporaryPath = cachedPath + ".tmp";
            await File.WriteAllBytesAsync(temporaryPath, bytes, cancellationToken);
            File.Move(temporaryPath, cachedPath, true);
            return cachedPath;
        }
        catch (Exception exception) when (
            exception is HttpRequestException
                or TaskCanceledException
                or IOException
                or System.Xml.XmlException)
        {
            return null;
        }
    }

    private static bool IsTrustedSteamImageHost(string host) =>
        host.EndsWith(".steamstatic.com", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".akamaihd.net", StringComparison.OrdinalIgnoreCase);
}
