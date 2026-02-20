namespace CodeSentinel.API.Models;

public sealed class StreamToken
{
    public string Token { get; set; } = string.Empty;
    public bool Done { get; set; }
    public List<string>? Sources { get; set; }
}
