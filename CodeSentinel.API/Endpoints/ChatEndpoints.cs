using CodeSentinel.API.Json;
using CodeSentinel.API.Models;
using CodeSentinel.API.Services;
using System.Text;
using System.Text.Json;

namespace CodeSentinel.API.Endpoints;

internal static class ChatEndpoints
{
    internal static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/chat", async (ChatRequest request, PromptOrchestrator orchestrator) =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await orchestrator.HandleAsync(request);
            result.LatencyMs = sw.ElapsedMilliseconds;
            return Results.Ok(result);
        });

        app.MapPost("/api/chat/stream", async (ChatRequest request, PromptOrchestrator orchestrator, HttpContext ctx, CancellationToken ct) =>
        {
            try
            {
                ctx.Response.Headers.ContentType = "text/event-stream";
                ctx.Response.Headers.CacheControl = "no-cache";
                ctx.Response.Headers.Connection = "keep-alive";

                var writer = ctx.Response.BodyWriter;

                await foreach (var token in orchestrator.StreamAsync(request, ct))
                {
                    var json = JsonSerializer.Serialize(token, AppJsonContext.Default.StreamToken);
                    var line = $"data: {json}\n\n";

                    await writer.WriteAsync(Encoding.UTF8.GetBytes(line), ct);
                    await writer.FlushAsync(ct);
                }
            }
            catch (Exception e)
            {
                throw;
            }
        });

        app.MapPost("/api/index", async (CodeChunk chunk, EmbeddingService embedder, VectorStore store) =>
        {
            chunk.Embedding = await embedder.GetEmbeddingAsync(chunk.Content);
            await store.UpsertAsync(chunk);
            return Results.NoContent();
        });

        return app;
    }
}
