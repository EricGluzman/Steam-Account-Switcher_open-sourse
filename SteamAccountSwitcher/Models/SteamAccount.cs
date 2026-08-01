using System.Text.Json.Serialization;

namespace SteamAccountSwitcher.Models;

public enum LoginMode
{
    RememberedSession,
    EncryptedPassword
}

public sealed class SteamAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Username { get; set; } = "";
    public string? SteamId64 { get; set; }
    public LoginMode LoginMode { get; set; }
    public string? EncryptedPassword { get; set; }
    [JsonIgnore]
    public bool IsCurrent { get; set; }
    [JsonIgnore]
    public string? AvatarPath { get; set; }

    public string Initial => string.IsNullOrWhiteSpace(DisplayName)
        ? "?"
        : DisplayName.Trim()[0].ToString().ToUpperInvariant();

    public string ModeLabel => LoginMode == LoginMode.RememberedSession
        ? "STEAM SAVED"
        : "ENCRYPTED LOGIN";
}

public sealed class AppSettings
{
    public bool ConfirmBeforeSwitch { get; set; } = true;
    public bool ShowSteamAccountChooser { get; set; }
    public string? SteamPath { get; set; }
}

public sealed class AppDataDocument
{
    public List<SteamAccount> Accounts { get; set; } = [];
    public AppSettings Settings { get; set; } = new();
}

public sealed class RememberedSteamAccount
{
    public required string SteamId64 { get; init; }
    public required string AccountName { get; init; }
    public string PersonaName { get; init; } = "";
    public bool RememberPassword { get; init; }

    public string Label => string.IsNullOrWhiteSpace(PersonaName)
        ? AccountName
        : $"{PersonaName}  ({AccountName})";
}
