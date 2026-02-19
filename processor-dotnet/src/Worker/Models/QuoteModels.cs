using System.Text.Json.Serialization;

namespace Worker.Models;

public sealed class QuoteItem
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = default!;

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("change_pct")]
    public decimal ChangePct { get; set; }

    [JsonPropertyName("quoted_at")]
    public DateTimeOffset QuotedAt { get; set; }
}

public sealed class QuoteResponse
{
    [JsonPropertyName("generated_at")]
    public DateTimeOffset GeneratedAt { get; set; }

    [JsonPropertyName("quotes")]
    public List<QuoteItem> Quotes { get; set; } = new();
}