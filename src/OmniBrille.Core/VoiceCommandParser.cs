using System.Text;

namespace OmniBrille.Core;

public sealed class VoiceCommandParser
{
    private static readonly Dictionary<string, VoiceIntentKind> ExactCommands =
        new Dictionary<string, VoiceIntentKind>(StringComparer.Ordinal)
        {
            ["go back"] = VoiceIntentKind.GoBack,
            ["back"] = VoiceIntentKind.GoBack,
            ["previous"] = VoiceIntentKind.GoBack,
            ["zoom in"] = VoiceIntentKind.ZoomIn,
            ["zoom out"] = VoiceIntentKind.ZoomOut,
            ["reset view"] = VoiceIntentKind.ResetView,
            ["reset the view"] = VoiceIntentKind.ResetView,
            ["switch to structure"] = VoiceIntentKind.SwitchToStructure,
            ["structure mode"] = VoiceIntentKind.SwitchToStructure,
            ["show structure"] = VoiceIntentKind.SwitchToStructure,
            ["switch to context"] = VoiceIntentKind.SwitchToContext,
            ["context mode"] = VoiceIntentKind.SwitchToContext,
            ["show context"] = VoiceIntentKind.SwitchToContext,
            ["show what is related to this"] = VoiceIntentKind.ShowRelatedToFocus,
            ["show related to this"] = VoiceIntentKind.ShowRelatedToFocus,
            ["what is related to this"] = VoiceIntentKind.ShowRelatedToFocus,
            ["use dark mode"] = VoiceIntentKind.UseDarkTheme,
            ["dark mode"] = VoiceIntentKind.UseDarkTheme,
            ["use dark theme"] = VoiceIntentKind.UseDarkTheme,
            ["use light mode"] = VoiceIntentKind.UseLightTheme,
            ["light mode"] = VoiceIntentKind.UseLightTheme,
            ["use light theme"] = VoiceIntentKind.UseLightTheme,
            ["open details"] = VoiceIntentKind.OpenDetails,
            ["show details"] = VoiceIntentKind.OpenDetails,
            ["close details"] = VoiceIntentKind.CloseDetails,
            ["hide details"] = VoiceIntentKind.CloseDetails,
            ["show list"] = VoiceIntentKind.ShowAccessibleList,
            ["open list"] = VoiceIntentKind.ShowAccessibleList,
            ["show accessible list"] = VoiceIntentKind.ShowAccessibleList,
            ["hide list"] = VoiceIntentKind.HideAccessibleList,
            ["close list"] = VoiceIntentKind.HideAccessibleList,
            ["hide accessible list"] = VoiceIntentKind.HideAccessibleList,
            ["clear search"] = VoiceIntentKind.ClearSearch,
            ["cancel"] = VoiceIntentKind.Cancel,
            ["stop"] = VoiceIntentKind.Cancel,
        };

    private static readonly string[] SearchPrefixes =
    [
        "search for ",
        "show me ",
        "find ",
        "look for ",
    ];

    public static VoiceIntent Parse(string transcript)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transcript);
        var trimmed = transcript.Trim();
        var normalized = NormalizeForComparison(TrimTerminalPunctuation(trimmed));
        if (normalized.Length == 0)
        {
            return new VoiceIntent(VoiceIntentKind.Search, string.Empty);
        }

        if (ExactCommands.TryGetValue(normalized, out var kind))
        {
            return new VoiceIntent(kind);
        }

        if (TryArgumentCommand(trimmed, normalized, "open ", VoiceIntentKind.OpenVisibleNode, out var open))
        {
            return open;
        }

        if (TryArgumentCommand(trimmed, normalized, "focus ", VoiceIntentKind.FocusVisibleNode, out var focus))
        {
            return focus;
        }

        foreach (var prefix in SearchPrefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.Ordinal) && normalized.Length > prefix.Length)
            {
                return new VoiceIntent(VoiceIntentKind.Search, ExtractOriginalArgument(trimmed, prefix.Length));
            }
        }

        return new VoiceIntent(VoiceIntentKind.Search, TrimTerminalPunctuation(trimmed));
    }

    public static string NormalizeForComparison(string value)
    {
        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character) || character is '-' or '_' or '.')
            {
                if (pendingSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(character);
                pendingSpace = false;
            }
            else
            {
                pendingSpace = true;
            }
        }

        return builder.ToString();
    }

    private static bool TryArgumentCommand(
        string original,
        string normalized,
        string prefix,
        VoiceIntentKind kind,
        out VoiceIntent intent)
    {
        if (normalized.StartsWith(prefix, StringComparison.Ordinal) && normalized.Length > prefix.Length)
        {
            intent = new VoiceIntent(kind, ExtractOriginalArgument(original, prefix.Length));
            return true;
        }

        intent = default!;
        return false;
    }

    private static string ExtractOriginalArgument(string original, int normalizedPrefixLength)
    {
        var firstSpace = original.IndexOf(' ');
        if (firstSpace < 0 || firstSpace + 1 >= original.Length)
        {
            return string.Empty;
        }

        if (normalizedPrefixLength > "find ".Length &&
            original.StartsWith("search", StringComparison.OrdinalIgnoreCase))
        {
            var forIndex = original.IndexOf("for", StringComparison.OrdinalIgnoreCase);
            if (forIndex >= 0 && forIndex + 3 < original.Length)
            {
                return TrimTerminalPunctuation(original[(forIndex + 3)..].Trim());
            }
        }

        if (original.StartsWith("show me", StringComparison.OrdinalIgnoreCase))
        {
            return TrimTerminalPunctuation(original["show me".Length..].Trim());
        }

        return TrimTerminalPunctuation(original[(firstSpace + 1)..].Trim());
    }

    private static string TrimTerminalPunctuation(string value) => value.Trim().TrimEnd('.', ',', '?', '!', ';', ':');
}
