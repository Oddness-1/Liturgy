using System;
using System.Collections.Generic;
using static Liturgy.Rite;

namespace Liturgy.Internal;

internal sealed partial class LiturgyNode
{
    /// <summary>
    /// Normalizes an Exclude pattern. Patterns may contain '*' (single-segment) and '**' (subtree)
    /// wildcards. Patterns starting with a wildcard are left unanchored for free-floating match;
    /// other patterns are anchored under /root/ via the standard NormalizePath rules.
    /// </summary>
    internal static string NormalizeExcludePattern(string pattern)
    {
        pattern = pattern.Trim();
        if (pattern.StartsWith('*')) return pattern;
        return NormalizePath(pattern);
    }

    /// <summary>
    /// Returns true if the given path matches any pattern in the exclude set.
    /// Patterns may contain '*' (single segment) and '**' (zero or more segments) wildcards.
    /// </summary>
    internal static bool PathMatchesAnyExclude(string path, HashSet<string>? excludes)
    {
        if (excludes is null) return false;
        foreach (var pattern in excludes)
        {
            if (PathMatchesExclude(path, pattern)) return true;
        }
        return false;
    }

    private static bool PathMatchesExclude(string path, string pattern)
    {
        // Fast path: no wildcards, exact match (backward compatible).
        if (!pattern.Contains('*')) return path == pattern;

        var patternSegments = pattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return MatchSegments(pathSegments, 0, patternSegments, 0);
    }

    private static bool MatchSegments(string[] path, int pi, string[] pattern, int ti)
    {
        while (ti < pattern.Length)
        {
            if (pattern[ti] == "**")
            {
                if (ti == pattern.Length - 1) return true;
                for (var consume = 0; pi + consume <= path.Length; consume++)
                {
                    if (MatchSegments(path, pi + consume, pattern, ti + 1)) return true;
                }
                return false;
            }

            if (pattern[ti] == "*")
            {
                if (pi >= path.Length) return false;
                pi++;
                ti++;
                continue;
            }

            if (pi >= path.Length || path[pi] != pattern[ti]) return false;
            pi++;
            ti++;
        }

        return pi == path.Length;
    }
}