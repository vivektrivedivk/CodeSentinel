using CodeSentinel.API.Json;
using CodeSentinel.API.Models;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace CodeSentinel.API.Services;

/// <summary>
/// Thin wrapper around Ollama's /api/generate endpoint.
/// Supports both streaming (IAsyncEnumerable) and one-shot modes.
/// AOT-safe: all (de)serialisation uses AppJsonContext.
/// </summary>
public sealed class LocalLlmService
{
    private const string Model = "deepseek-coder:6.7b";   // swap to llama3, phi3, etc.
    private readonly HttpClient _http;

    public LocalLlmService(HttpClient http) => _http = http;

    /// <summary>
    /// Yields one string token per Ollama stream chunk.
    /// The final yielded string is empty — check Done on the chunk yourself
    /// if you need the signal; for callers that just want tokens, ignore empties.
    /// </summary>
    public async IAsyncEnumerable<string> StreamAsync(string prompt, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var body = new OllamaGenerateRequest
        {
            Model = Model,
            Prompt = prompt,
            Stream = true,
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
        {
            Content = JsonContent.Create(body, AppJsonContext.Default.OllamaGenerateRequest),
        };

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        try
        {
            resp.EnsureSuccessStatusCode();

        }
        catch (Exception e)
        {

            throw;
        }

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;

            var chunk = JsonSerializer.Deserialize(line, AppJsonContext.Default.OllamaGenerateStreamChunk);
            if (chunk is null) continue;

            if (!string.IsNullOrEmpty(chunk.Response))
                yield return chunk.Response;

            if (chunk.Done) yield break;
        }
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        var sb = new System.Text.StringBuilder();
        await foreach (var token in StreamAsync(prompt, ct))
            sb.Append(token);
        return sb.ToString();
    }
}
