using CodeSentinel.API.Models;

namespace CodeSentinel.API.Services;

/// <summary>
/// Retrieval-Augmented Generation pipeline:
/// embed query → cosine search → return ranked chunks.
/// </summary>
public sealed class RagService
{
    private readonly EmbeddingService _embedder;
    private readonly VectorStore _store;

    public RagService(EmbeddingService embedder, VectorStore store)
    {
        _embedder = embedder;
        _store = store;
    }

    public async Task<List<CodeChunk>> GetRelevantContextAsync(string query, int topK = 5, CancellationToken ct = default)
    {
        var embedding = await _embedder.GetEmbeddingAsync(query, ct);
        return await _store.SearchAsync(embedding, topK);
    }
}
