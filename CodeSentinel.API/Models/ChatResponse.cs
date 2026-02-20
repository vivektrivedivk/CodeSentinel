namespace CodeSentinel.API.Models;

public sealed class ChatResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<string> Sources { get; set; } = [];
    public long LatencyMs { get; set; }
}
