namespace Worker.Models;

public sealed class QuoteEnvelope
{
    public string generated_at { get; set; } = default!;
    public List<Quote> quotes { get; set; } = new();
}

public sealed class Quote
{
    public string symbol { get; set; } = default!;
    public decimal price { get; set; }
    public decimal change_pct { get; set; }
    public DateTimeOffset quoted_at { get; set; }
}