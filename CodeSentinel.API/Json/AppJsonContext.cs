using CodeSentinel.API.Models;
using System.Text.Json.Serialization;

namespace CodeSentinel.API.Json;

/// <summary>
/// Single source-generation context for the entire API surface.
/// All Ollama request/response types, domain models, and streaming tokens live here.
/// Adding a new serializable type = add a [JsonSerializable] attribute. Nothing else.
/// </summary>
[JsonSerializable(typeof(ChatRequest))]
[JsonSerializable(typeof(ChatResponse))]
[JsonSerializable(typeof(CodeChunk))]
[JsonSerializable(typeof(StreamToken))]
[JsonSerializable(typeof(OllamaGenerateRequest))]
[JsonSerializable(typeof(OllamaGenerateStreamChunk))]
[JsonSerializable(typeof(OllamaEmbedRequest))]
[JsonSerializable(typeof(OllamaEmbedResponse))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
internal sealed partial class AppJsonContext : JsonSerializerContext { }
