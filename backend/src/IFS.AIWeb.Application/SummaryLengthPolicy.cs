using System.Text;

namespace IFS.AIWeb.Application;

public sealed record SummaryLengthProfile(
    string Id,
    string TurkishLabel,
    int NormalizedSourceLengthRunes,
    int BandHardMaximumRunes,
    int EffectiveMaximumRunes,
    string PromptGuidance);

public interface ISummaryLengthPolicy
{
    SummaryLengthProfile Select(string sourceText);
}

public sealed class SummaryLengthPolicy : ISummaryLengthPolicy
{
    public SummaryLengthProfile Select(string sourceText)
    {
        var sourceLength = NormalizedRuneCount(sourceText);
        var (id, label, maximum, guidance) = sourceLength switch
        {
            <= 200 => ("very_short", "Çok kısa", 160,
                "Use one brief sentence; preserve only the essential proposition and never pad."),
            <= 1_000 => ("short", "Kısa", 360,
                "Use approximately 1-3 concise sentences, normally about 100-300 characters."),
            <= 4_000 => ("medium", "Orta", 800,
                "Provide a well-structured summary of approximately 4-7 informative sentences (300-650 characters), capturing key events, dates, core facts, and main points without omitting important context."),
            _ => ("long", "Uzun", 1_500,
                "Provide a comprehensive, highly detailed summary of approximately 5-10 cohesive sentences (500-1,200 characters), capturing all major dates, events, key entities, and important decisions in full depth.")
        };
        return new(id, label, sourceLength, maximum, maximum, guidance);
    }

    private static int NormalizedRuneCount(string sourceText)
    {
        var count = 0;
        var pendingSpace = false;
        foreach (var rune in sourceText.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                if (count > 0) pendingSpace = true;
                continue;
            }
            if (pendingSpace) { count++; pendingSpace = false; }
            count++;
        }
        return count;
    }
}
