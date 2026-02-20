using CodeSentinel.API.Models;
using System.Runtime.CompilerServices;
using System.Text;

namespace CodeSentinel.API.Services;

/// <summary>
/// Coordinates RAG retrieval, prompt construction, and LLM generation.
/// Exposes both a one-shot path and a streaming path (IAsyncEnumerable).
/// </summary>
public sealed class PromptOrchestrator
{
    private readonly LocalLlmService _llm;
    private readonly RagService _rag;

    public PromptOrchestrator(LocalLlmService llm, RagService rag)
    {
        _llm = llm;
        _rag = rag;
    }

    public async Task<ChatResponse> HandleAsync(ChatRequest request, CancellationToken ct = default)
    {
        var chunks = await _rag.GetRelevantContextAsync(request.Query, request.TopK, ct);
        var prompt = BuildPrompt(request.Query, chunks);
        var answer = await _llm.GenerateAsync(prompt, ct);

        return new ChatResponse
        {
            Answer = answer,
            Sources = chunks.Select(c => c.FilePath).Distinct().ToList(),
        };
    }

    /// <summary>
    /// Yields StreamToken frames suitable for direct SSE emission.
    /// Retrieval happens upfront (blocking); tokens stream as the LLM produces them.
    /// The final frame carries Done=true and the source list.
    /// </summary>
    public async IAsyncEnumerable<StreamToken> StreamAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var chunks = await _rag.GetRelevantContextAsync(request.Query, request.TopK, ct);
        var prompt = BuildPrompt(request.Query, chunks);
        var sources = chunks.Select(c => c.FilePath).Distinct().ToList();

        await foreach (var token in _llm.StreamAsync(prompt, ct))
        {
            yield return new StreamToken { Token = token };
        }

        yield return new StreamToken { Done = true, Sources = sources };
    }

    private static string BuildPrompt(string query, List<CodeChunk> chunks)
    {
        if (chunks.Count == 0)
        {
            return $"""
                You are a senior .NET architect and code reviewer.
                No relevant code context was found in the indexed codebase.

                Question:
                {query}

                Answer as precisely as possible based on your knowledge.
                """;
        }

        var sb = new StringBuilder(capacity: 4096);
        sb.AppendLine("You are a senior .NET architect and code reviewer.");
        sb.AppendLine("Answer the question using ONLY the code context provided below.");
        sb.AppendLine("If the answer cannot be derived from the context, say so explicitly.");
        sb.AppendLine();
        sb.AppendLine("=== CODE CONTEXT ===");

        foreach (var chunk in chunks)
        {
            sb.AppendLine($"// {chunk.FilePath}");
            sb.AppendLine(chunk.Content);
            sb.AppendLine("---");
        }

        sb.AppendLine("=== QUESTION ===");
        sb.AppendLine(query);

        return sb.ToString();
    }
}
