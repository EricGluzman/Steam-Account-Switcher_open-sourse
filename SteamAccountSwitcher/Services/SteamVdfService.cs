using System.Text;
using System.Text.RegularExpressions;
using SteamAccountSwitcher.Models;

namespace SteamAccountSwitcher.Services;

public sealed class SteamVdfService
{
    private static readonly Regex QuotedValue = new(
        "\"(?<key>[^\"]+)\"\\s+\"(?<value>[^\"]*)\"",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public IReadOnlyList<RememberedSteamAccount> ReadAccounts(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        var content = File.ReadAllText(filePath);
        return FindAccountBlocks(content)
            .Select(block =>
            {
                var values = QuotedValue.Matches(block.Content)
                    .ToDictionary(
                        match => match.Groups["key"].Value,
                        match => match.Groups["value"].Value,
                        StringComparer.OrdinalIgnoreCase);
                return values.TryGetValue("AccountName", out var accountName)
                    ? new RememberedSteamAccount
                    {
                        SteamId64 = block.SteamId64,
                        AccountName = accountName,
                        PersonaName = values.GetValueOrDefault("PersonaName") ?? "",
                        RememberPassword = values.GetValueOrDefault("RememberPassword") == "1"
                    }
                    : null;
            })
            .OfType<RememberedSteamAccount>()
            .ToList();
    }

    public void ActivateAccount(string filePath, string steamId64)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Steam's remembered-account file was not found.", filePath);
        }

        var original = File.ReadAllText(filePath);
        var updated = TransformForActivation(original, steamId64);
        var backupPath = filePath + ".switcher.bak";
        var temporaryPath = filePath + ".switcher.tmp";

        File.Copy(filePath, backupPath, true);
        File.WriteAllText(temporaryPath, updated, new UTF8Encoding(false));
        File.Move(temporaryPath, filePath, true);
    }

    public bool SetUserChooser(string configPath, bool showChooser)
    {
        if (!File.Exists(configPath))
        {
            return false;
        }

        var original = File.ReadAllText(configPath);
        var updated = TransformUserChooser(original, showChooser);
        if (updated == original)
        {
            return false;
        }

        var backupPath = configPath + ".switcher.bak";
        var temporaryPath = configPath + ".switcher.tmp";
        File.Copy(configPath, backupPath, true);
        File.WriteAllText(temporaryPath, updated, new UTF8Encoding(false));
        File.Move(temporaryPath, configPath, true);
        return true;
    }

    public string TransformUserChooser(string content, bool showChooser)
    {
        var chooserPattern = new Regex(
            "(?im)^(?<indent>\\s*)\"(?<key>AlwaysShowUserChooser)\"(?<gap>\\s+)\"[01]\"",
            RegexOptions.CultureInvariant);
        var value = showChooser ? "1" : "0";
        return chooserPattern.Replace(
            content,
            match => $"{match.Groups["indent"].Value}\"{match.Groups["key"].Value}\"{match.Groups["gap"].Value}\"{value}\"",
            1);
    }

    public string TransformForActivation(string content, string steamId64)
    {
        var blocks = FindAccountBlocks(content);
        if (!blocks.Any(block => block.SteamId64 == steamId64))
        {
            throw new InvalidOperationException("That Steam account is no longer present in loginusers.vdf.");
        }

        var builder = new StringBuilder(content);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        foreach (var block in blocks.OrderByDescending(item => item.ContentStart))
        {
            var isTarget = block.SteamId64 == steamId64;
            var replacement = SetField(block.Content, "MostRecent", isTarget ? "1" : "0");
            replacement = SetField(replacement, "AutoLogin", isTarget ? "1" : "0");
            if (isTarget)
            {
                replacement = SetField(replacement, "Timestamp", timestamp);
                replacement = SetField(replacement, "RememberPassword", "1");
                replacement = SetField(replacement, "WantsOfflineMode", "0");
                replacement = SetField(replacement, "SkipOfflineModeWarning", "0");
            }

            builder.Remove(block.ContentStart, block.ContentLength);
            builder.Insert(block.ContentStart, replacement);
        }

        return builder.ToString();
    }

    private static string SetField(string block, string key, string value)
    {
        var pattern = new Regex(
            $"(?im)^(?<indent>\\s*)\"(?<key>{Regex.Escape(key)})\"(?<gap>\\s+)\"[^\"]*\"",
            RegexOptions.CultureInvariant);
        if (pattern.IsMatch(block))
        {
            return pattern.Replace(
                block,
                match => $"{match.Groups["indent"].Value}\"{match.Groups["key"].Value}\"{match.Groups["gap"].Value}\"{value}\"",
                1);
        }

        var closingBrace = block.LastIndexOf('}');
        if (closingBrace < 0)
        {
            throw new FormatException("Steam's loginusers.vdf contains an invalid account block.");
        }

        return block.Insert(closingBrace, $"\t\t\"{key}\"\t\t\"{value}\"{Environment.NewLine}");
    }

    private static List<AccountBlock> FindAccountBlocks(string content)
    {
        var blocks = new List<AccountBlock>();
        var header = new Regex("\"(?<id>\\d{15,20})\"\\s*\\{", RegexOptions.CultureInvariant);
        foreach (Match match in header.Matches(content))
        {
            var openBrace = content.IndexOf('{', match.Index + match.Length - 1);
            var depth = 0;
            var inString = false;
            var escaped = false;
            for (var index = openBrace; index < content.Length; index++)
            {
                var character = content[index];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (character == '\\')
                    {
                        escaped = true;
                    }
                    else if (character == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (character == '"')
                {
                    inString = true;
                }
                else if (character == '{')
                {
                    depth++;
                }
                else if (character == '}' && --depth == 0)
                {
                    blocks.Add(new AccountBlock(
                        match.Groups["id"].Value,
                        openBrace,
                        index - openBrace + 1,
                        content[openBrace..(index + 1)]));
                    break;
                }
            }
        }

        return blocks;
    }

    private sealed record AccountBlock(
        string SteamId64,
        int ContentStart,
        int ContentLength,
        string Content);
}
