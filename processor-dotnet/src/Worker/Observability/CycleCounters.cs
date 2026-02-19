namespace Worker.Observability;

public sealed class CycleCounters
{
    public int SymbolsRequested { get; set; }
    public int QuotesFetched { get; set; }
    public int QuotesInserted { get; set; }
    public int QuotesConflictSkipped { get; set; }
    public int Retries { get; set; }
    public int Errors { get; set; }
}