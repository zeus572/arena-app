namespace Arena.Shared.News;

/// <summary>
/// A raw headline fetched from an upstream source. Consumers persist this as
/// their own database row (with whatever provenance/processing-status fields
/// they need); this type is the wire shape between fetcher and consumer.
/// </summary>
public record NewsItem(
    string ExternalId,
    string Headline,
    string Source,
    string Url,
    string? Summary,
    DateTime PublishedAt);
