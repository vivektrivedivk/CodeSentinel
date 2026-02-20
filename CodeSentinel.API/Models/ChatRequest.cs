namespace CodeSentinel.API.Models;

public sealed class ChatRequest
{
    public string Query { get; set; } = string.Empty;
    public int TopK { get; set; } = 5;
}
