using System.Text.RegularExpressions;
using SpeakSharp.Models;

namespace SpeakSharp.Services;

public sealed partial class FillerAnalysisService
{
    private static readonly (string Label, Regex Pattern)[] Patterns =
    [
        ("um / uhm", UmRegex()),
        ("uh / er / ah", HesitationRegex()),
        ("you know", YouKnowRegex()),
        ("I mean", IMeanRegex()),
        ("basically", BasicallyRegex()),
        ("actually", ActuallyRegex()),
        ("so", SoRegex()),
        ("like", LikeRegex()),
    ];

    public SpeechAnalysis Analyze(string transcript, TimeSpan duration)
    {
        var words = WordRegex().Matches(transcript).Count;
        var minutes = Math.Max(duration.TotalMinutes, 1d / 60d);
        var details = Patterns
            .Select(p => new FillerBreakdown(p.Label, p.Pattern.Matches(transcript).Count))
            .Where(x => x.Count > 0)
            .OrderByDescending(x => x.Count)
            .ToArray();
        var fillers = details.Sum(x => x.Count);
        var rate = words == 0 ? 0 : fillers * 100d / words;
        var pace = words / minutes;
        var pacePenalty = pace switch { < 80 => 12, > 190 => 15, > 165 => 7, _ => 0 };
        var fillerPenalty = Math.Min(45, (int)Math.Round(rate * 5));

        return new SpeechAnalysis
        {
            WordCount = words,
            WordsPerMinute = Math.Round(pace),
            FillerCount = fillers,
            FillerRate = Math.Round(rate, 1),
            Score = Math.Clamp(100 - pacePenalty - fillerPenalty, 0, 100),
            Fillers = details,
        };
    }

    [GeneratedRegex(@"\b(?:um+|uhm+)\b", RegexOptions.IgnoreCase)] private static partial Regex UmRegex();
    [GeneratedRegex(@"\b(?:uh+|er+|ah+|erm+)\b", RegexOptions.IgnoreCase)] private static partial Regex HesitationRegex();
    [GeneratedRegex(@"\byou\s+know\b", RegexOptions.IgnoreCase)] private static partial Regex YouKnowRegex();
    [GeneratedRegex(@"\bi\s+mean\b", RegexOptions.IgnoreCase)] private static partial Regex IMeanRegex();
    [GeneratedRegex(@"\bbasically\b", RegexOptions.IgnoreCase)] private static partial Regex BasicallyRegex();
    [GeneratedRegex(@"\bactually\b", RegexOptions.IgnoreCase)] private static partial Regex ActuallyRegex();
    [GeneratedRegex(@"\bso\b", RegexOptions.IgnoreCase)] private static partial Regex SoRegex();
    [GeneratedRegex(@"\blike\b", RegexOptions.IgnoreCase)] private static partial Regex LikeRegex();
    [GeneratedRegex(@"[\p{L}\p{N}']+")] private static partial Regex WordRegex();
}
