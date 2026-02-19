using System.Text.Json.Serialization;

namespace Worker.Models;

public sealed class PrecoResponse
{
    [JsonPropertyName("ativo")]
    public string Ativo { get; set; } = default!;

    [JsonPropertyName("preco")]
    public decimal Preco { get; set; }
}