namespace CodeSentinel.API.Models;

public sealed class OllamaGenerateRequest
{
    public string Model { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public bool Stream { get; set; }
}

/// <summary>
/// Ollama streams newline-delimited JSON objects.
/// Each line deserialises to this type.
/// </summary>
public sealed class OllamaGenerateStreamChunk
{
    public string Response { get; set; } = string.Empty;
    public bool Done { get; set; }
}

public sealed class OllamaEmbedRequest
{
    public string Model { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
}

public sealed class OllamaEmbedResponse
{
    public float[][] Embeddings { get; set; } = [];
}
