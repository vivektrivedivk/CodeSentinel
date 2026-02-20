namespace CodeSentinel.API.Models;

public sealed class CodeChunk
{
    public string Id { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public float[]? Embedding { get; set; }
}
