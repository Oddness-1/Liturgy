using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Liturgy.Internal;

/// <summary>
/// The harness's words: the greeting, what's new in each version, and the guide.
/// </summary>
internal static class HarnessText
{
    public static readonly string VERSION = ReadVersion();

    // CHANGELOG.md from the repository root, embedded at build time. Each release adds a section to it.
    private const string CHANGELOG_RESOURCE = "Liturgy.CHANGELOG.md";

    private static List<(string Version, string Notes)>? _changelog;

    public static string GreetingTitle(string? seenVersion) => seenVersion is null or "" ? "Liturgy is running" : $"Liturgy {VERSION}";

    public static string GreetingBody(string? seenVersion)
    {
        const string HARNESS = "Press Alt+~ at any time to open the harness: every rite in your game, the nodes each one touches, and a live log of what fires.";

        return seenVersion is null or ""
            ? $"{HARNESS} It's only in runs from the editor; exported games never include it."
            : $"Updated from {seenVersion}. Alt+~ opens the harness.";
    }

    /// <summary>
    /// The changelog sections newer than <paramref name="seenVersion"/>, newest first, as BBCode.
    /// </summary>
    public static string ChangesSince(string? seenVersion, string gold)
    {
        var seen = Parse(seenVersion);
        var text = new StringBuilder();

        foreach (var (version, notes) in Changelog())
        {
            if (seen is not null && Parse(version) is { } parsed && parsed <= seen) continue;
            text.Append($"[b][color=#{gold}]{version}[/color][/b]\n{ToBbcode(notes)}\n\n");
        }

        return text.ToString().TrimEnd();
    }

    private static List<(string Version, string Notes)> Changelog()
    {
        if (_changelog is not null) return _changelog;
        _changelog = [];

        using var stream = typeof(HarnessText).Assembly.GetManifestResourceStream(CHANGELOG_RESOURCE);
        if (stream is null) return _changelog;

        string? version = null;
        var notes = new StringBuilder();

        foreach (var line in new StreamReader(stream).ReadToEnd().Split('\n').Select(line => line.TrimEnd('\r')))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                if (version is not null) _changelog.Add((version, notes.ToString().Trim()));
                version = line[3..].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim('[', ']');
                notes.Clear();
            }
            else if (version is not null) notes.AppendLine(line);
        }

        if (version is not null) _changelog.Add((version, notes.ToString().Trim()));
        return _changelog;
    }

    private static string ToBbcode(string markdown) => string.Join('\n', markdown.Split('\n').Select(line =>
    {
        var text = line.Replace("[", "[lb]");
        text = System.Text.RegularExpressions.Regex.Replace(text, "`([^`]+)`", "[code]$1[/code]");
        return text.StartsWith("- ", StringComparison.Ordinal) ? $"  •  {text[2..]}" : text;
    }));

    private static Version? Parse(string? version) =>
        System.Version.TryParse(version?.Split('-', '+')[0], out var parsed) ? parsed : null;

    public static string Guide(string gold, string dim) => $"""
        [b][color=#{gold}]What you're looking at[/color][/b]
        Every method in your game that carries a rite, what it does, which nodes it acts on, and when it last ran. Nothing here is guessed from names: it's read from the rites Liturgy actually wired up.

        [b][color=#{gold}]Rites[/color][/b]
        One row per method. [b]Nodes[/b] is how many nodes in the tree it acts on right now, or [i]game[/i] for rites that act on the game as a whole (input, tree changes, global frames). [b]Fires[/b] counts every run, and [b]Time[/b] is how long all of them took together. Select a row to outline its nodes in blue. Untick it to mute it: Liturgy skips it until you tick it again, so you can see what changes without it. A red row is a rite Liturgy rejected at startup; the console says why.

        [b][color=#{gold}]Nodes[/color][/b]
        The scene tree, with how many rites act on each node. [b]Pick on screen[/b] lets you click a control in your game to jump to it. The details list every rite acting on the node, its description, and how often it has fired for that node.

        [b][color=#{gold}]Assigned[/color][/b]
        Every [b]AssignNode[/b] member, the path it follows, and the node it holds. A member is emptied when its node leaves the tree and filled again when a node arrives at its path; when it's empty, the row says why. Select a row to outline its node.

        [b][color=#{gold}]Log[/color][/b]
        The latest firings, newest at the bottom, with the node each ran for. Per-frame rites are hidden by default. Click a line to inspect its node.

        [b][color=#{gold}]Arranging it[/color][/b]
        Click a heading to sort by it, and again to reverse. In the node tree, a third click goes back to scene order. Drag the line between two headings to resize a column, the bar between two panes to resize them, and the panel's right edge to widen it. The harness remembers all of it for this project.

        [b][color=#{gold}]Describing your rites[/color][/b]
        Every rite takes an optional description, shown here:
        [color=#{dim}][lb]Rite.OnButtonPressed(Description = "Plays the click sound for every button.")][/color]

        [b][color=#{gold}]Keeping a scene out of it[/color][/b]
        Add a node to the [b]{Rite.IGNORE_GROUP}[/b] group (in its scene, or before it enters the tree) and no rite will see it or anything under it.

        [b][color=#{gold}]Settings[/color][/b]
        [color=#{dim}][lb]assembly: Rite.Settings(Harness = false)][/color] turns the harness off.
        [color=#{dim}][lb]assembly: Rite.Settings(HarnessPausesGame = true)][/color] pauses the game while it's open, for games that read Input directly every frame.

        [b][color=#{gold}]What it costs[/color][/b]
        In runs from the editor Liturgy counts and times each rite as it runs. Exported games skip all of it: no harness, no counting, rites are called directly.

        [b][color=#{gold}]Changelog[/color][/b]
        {ChangesSince(null, gold)}

        [color=#{dim}]Liturgy {VERSION}[/color]
        """;

    private static string ReadVersion()
    {
        var assembly = typeof(HarnessText).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (informational is not null) return informational.Split('+')[0];

        var version = assembly.GetName().Version;
        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
