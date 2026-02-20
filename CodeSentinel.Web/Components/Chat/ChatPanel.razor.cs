using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace CodeSentinel.Web.Components.Chat;

public sealed partial class ChatPanel : ComponentBase
{
    private record ChatMessage(string Text, bool IsUser, List<string>? Sources = null);

    private readonly List<ChatMessage> _messages = [];
    private string _query = string.Empty;
    private bool _streaming;
    private readonly StringBuilder _streamBuffer = new();
    private ElementReference _messageListRef;

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !e.ShiftKey) await SendAsync();
    }

    private async Task SendAsync()
    {
        var query = _query.Trim();
        if (string.IsNullOrEmpty(query) || _streaming) return;

        _query = string.Empty;
        _messages.Add(new ChatMessage(query, IsUser: true));
        _streaming = true;
        _streamBuffer.Clear();
        StateHasChanged();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:5211/api/chat/stream")
            {
                Content = JsonContent.Create(new { Query = query, TopK = 5 }),
            };

            using var response = await Http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead);

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            List<string>? finalSources = null;


            string? line; while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var json = line.StartsWith("data: ", StringComparison.Ordinal) ? line["data: ".Length..] : line;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("token", out var tokenEl))
                    _streamBuffer.Append(tokenEl.GetString());

                if (root.TryGetProperty("done", out var doneEl) && doneEl.GetBoolean())
                {
                    if (root.TryGetProperty("sources", out var srcEl))
                    {
                        finalSources = srcEl.EnumerateArray()
                            .Select(s => s.GetString() ?? string.Empty)
                            .ToList();
                    }
                    break;
                }

                StateHasChanged();
                await Task.Yield();
            }

            _messages.Add(new ChatMessage(_streamBuffer.ToString(), IsUser: false, finalSources));
        }
        catch (Exception ex)
        {
            _messages.Add(new ChatMessage($"[Error] {ex.Message}", IsUser: false));
        }
        finally
        {
            _streaming = false;
            _streamBuffer.Clear();
            StateHasChanged();
            try
            {
                await JS.InvokeVoidAsync("scrollToBottom", _messageListRef);
            }
            catch
            {
                // JS function may not be available, ignore
            }
        }
    }
}
