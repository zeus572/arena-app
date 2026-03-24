using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Arena.API.Data;
using Arena.API.Models;

namespace Arena.API.Services;

public class TaggingService
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "is", "are", "was", "were", "be", "been", "being",
        "have", "has", "had", "do", "does", "did", "will", "would", "shall",
        "should", "may", "might", "must", "can", "could", "to", "of", "in",
        "for", "on", "with", "at", "by", "from", "as", "into", "through",
        "during", "before", "after", "above", "below", "between", "under",
        "and", "but", "or", "nor", "not", "so", "yet", "both", "either",
        "neither", "each", "every", "all", "any", "few", "more", "most",
        "other", "some", "such", "than", "too", "very", "just", "because",
        "if", "when", "where", "how", "what", "which", "who", "whom",
        "this", "that", "these", "those", "it", "its", "we", "they", "our",
        "their", "about", "up", "out", "no", "only", "own", "same",
        "also", "over", "whether", "viable", "adopted", "effective",
        "permitted", "justified", "regulated", "pursue", "fix",
    };

    public async Task ExtractAndAssignTagsAsync(ArenaDbContext db, Debate debate)
    {
        var words = debate.Topic
            .Split(new[] { ' ', '—', '–', '/', ':', '?', '!', ',', '.', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim().TrimEnd('\'', '"', ')').TrimStart('(', '"', '\''))
            .Where(w => w.Length > 1 && !StopWords.Contains(w))
            .ToList();

        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Bigrams
        for (var i = 0; i < words.Count - 1; i++)
        {
            candidates.Add($"{words[i]} {words[i + 1]}");
        }

        // Significant single words (5+ chars, not already in a bigram)
        foreach (var word in words.Where(w => w.Length >= 5))
        {
            candidates.Add(word);
        }

        // Remove single words that are subsumed by bigrams
        var bigrams = candidates.Where(c => c.Contains(' ')).ToList();
        candidates.RemoveWhere(c =>
            !c.Contains(' ') && bigrams.Any(b => b.Contains(c, StringComparison.OrdinalIgnoreCase)));

        foreach (var candidate in candidates)
        {
            var name = candidate.ToLowerInvariant();
            var displayName = ToTitleCase(candidate);

            var tag = await db.Tags.FirstOrDefaultAsync(t => t.Name == name);
            if (tag is null)
            {
                tag = new Tag { Name = name, DisplayName = displayName };
                db.Tags.Add(tag);
            }
            tag.UsageCount++;

            var exists = await db.DebateTags.AnyAsync(dt => dt.DebateId == debate.Id && dt.TagId == tag.Id);
            if (!exists && tag.Id > 0)
            {
                db.DebateTags.Add(new DebateTag { DebateId = debate.Id, TagId = tag.Id });
            }
            else if (tag.Id == 0)
            {
                // New tag — EF will set the ID after SaveChanges; add join via navigation
                db.DebateTags.Add(new DebateTag { Debate = debate, Tag = tag });
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task BackfillAllAsync(ArenaDbContext db)
    {
        var debates = await db.Debates.Include(d => d.DebateTags).ToListAsync();
        foreach (var debate in debates)
        {
            if (debate.DebateTags.Count > 0) continue;
            await ExtractAndAssignTagsAsync(db, debate);
        }
    }

    private static string ToTitleCase(string input)
    {
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(input.ToLowerInvariant());
    }
}
