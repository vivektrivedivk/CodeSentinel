using CodeSentinel.API.Json;
using CodeSentinel.API.Models;

namespace CodeSentinel.API.Services;

/// <summary>
/// Calls Ollama's /api/embed to produce a dense vector for a given text.
/// Model: nomic-embed-text (768-dim, fits comfortably under 8 GB RAM on CPU).
/// Alternative: all-minilm (384-dim, even lighter).
/// </summary>
public sealed class EmbeddingService
{
    private const string Model = "nomic-embed-text";
    private readonly HttpClient _http;

    public EmbeddingService(HttpClient http) => _http = http;

    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct = default)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(text);

            var req = new OllamaEmbedRequest { Model = Model, Input = text };

            using var resp = await _http.PostAsync(
                "/api/embed",
                JsonContent.Create(req, AppJsonContext.Default.OllamaEmbedRequest),
                ct);

            resp.EnsureSuccessStatusCode();

            var result = await resp.Content.ReadFromJsonAsync(
                AppJsonContext.Default.OllamaEmbedResponse, ct);

            return result?.Embeddings?.FirstOrDefault() ?? [];
        }
        catch (Exception e)
        {
            throw;
        }
    }
}
