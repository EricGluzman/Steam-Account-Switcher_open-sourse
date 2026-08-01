# Steam Account Switcher

A Windows desktop app for organizing and switching between Steam accounts.

## Requirements

- Windows 10 or 11
- Steam desktop client
- .NET 8 SDK only when building from source

## Build and publish

```powershell
dotnet build .\SteamAccountSwitcher\SteamAccountSwitcher.csproj
dotnet run --project .\SteamAccountSwitcher.Tests\SteamAccountSwitcher.Tests.csproj
dotnet publish .\SteamAccountSwitcher\SteamAccountSwitcher.csproj -c Release -r win-x64 --self-contained true
```

The self-contained executable is written to:

`SteamAccountSwitcher\bin\Release\net8.0-windows\win-x64\publish\SteamAccountSwitcher.exe`

## Login modes

- **Steam saved:** uses an account already listed in Steam's `config\loginusers.vdf`. Sign into that account once with **Remember me** enabled before adding it.
- **Encrypted login:** stores the password with Windows DPAPI, scoped to the current Windows user. Steam Guard can still prompt.

Steam's `-login` feature requires the password in the process command line. This means another process running as your Windows user could briefly inspect it during launch. Prefer Steam's remembered-session mode whenever possible.

The app closes Steam gracefully before changing the remembered account. It creates `loginusers.vdf.switcher.bak` before updating Steam's file and never force-kills Steam.

Removing a card only removes data saved by this app. It does not remove Steam's remembered account.
