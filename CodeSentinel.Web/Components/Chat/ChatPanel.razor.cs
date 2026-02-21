using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodeSentinel.Web.Components.Chat;

public sealed partial class ChatPanel : ComponentBase
{
    private record ChatMessage(string Id, string Text, bool IsUser, List<string>? Sources = null)
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsEditing { get; set; }
    }

    private readonly List<ChatMessage> _messages = new();
    private readonly HashSet<string> _expandedSources = new();
    private string _query = string.Empty;
    private string _editText = string.Empty;
    private bool _streaming;
    private readonly StringBuilder _streamBuffer = new();
    private ElementReference _messageListRef;
    private ElementReference _inputRef;
    private string? _errorMessage;
    private string _selectedModel = "deepseek-coder";
    private int _estimatedTokens = 0;
    private bool _showLabels = true;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await JS.InvokeVoidAsync("initChat", _messageListRef);
            await _inputRef.FocusAsync();
        }
    }

    private void OnInput(ChangeEventArgs e)
    {
        // Auto-resize textarea
        _ = JS.InvokeVoidAsync("autoResizeTextarea", _inputRef);
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !e.ShiftKey)
        {
            await SendAsync();
        }
        _ = JS.InvokeVoidAsync("autoResizeTextarea", _inputRef);
    }

    private async Task HandleEditKeyDown(KeyboardEventArgs e, ChatMessage msg)
    {
        if (e.Key == "Enter" && !e.ShiftKey)
        {
            await SaveEdit(msg);
        }
        else if (e.Key == "Escape")
        {
            CancelEdit(msg);
        }
    }

    private async Task SendAsync()
    {
        var query = _query.Trim();
        if (string.IsNullOrEmpty(query) || _streaming) return;

        _query = string.Empty;
        _errorMessage = null;

        var messageId = Guid.NewGuid().ToString("N")[..8];
        _messages.Add(new ChatMessage(messageId, query, IsUser: true));

        await ScrollToBottom();
        await StartStreaming(query);
    }

    private async Task StartStreaming(string query)
    {
        _streaming = true;
        _streamBuffer.Clear();
        StateHasChanged();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:5211/api/chat/stream")
            {
                Content = JsonContent.Create(new { Query = query, TopK = 5, Model = _selectedModel }),
            };

            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            List<string>? finalSources = null;
            string? line;

            while ((line = await reader.ReadLineAsync()) != null)
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
                            .Where(s => !string.IsNullOrEmpty(s))
                            .ToList();
                    }
                    break;
                }

                StateHasChanged();
                await Task.Yield();
            }

            var responseId = Guid.NewGuid().ToString("N")[..8];
            _messages.Add(new ChatMessage(responseId, _streamBuffer.ToString(), IsUser: false, finalSources));
            UpdateTokenCount();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to get response: {ex.Message}";
        }
        finally
        {
            _streaming = false;
            _streamBuffer.Clear();
            StateHasChanged();
            await ScrollToBottom();
            await _inputRef.FocusAsync();
        }
    }

    private async Task RegenerateResponse(ChatMessage msg)
    {
        // Find the user message that prompted this response
        var msgIndex = _messages.IndexOf(msg);
        if (msgIndex <= 0) return;

        var userMsg = _messages[msgIndex - 1];
        if (!userMsg.IsUser) return;

        // Remove the old response
        _messages.RemoveAt(msgIndex);
        StateHasChanged();

        // Regenerate
        await StartStreaming(userMsg.Text);
    }

    private void StartEdit(ChatMessage msg)
    {
        _editText = msg.Text;
        msg.IsEditing = true;
        StateHasChanged();
    }

    private void CancelEdit(ChatMessage msg)
    {
        msg.IsEditing = false;
        _editText = string.Empty;
        StateHasChanged();
    }

    private async Task SaveEdit(ChatMessage msg)
    {
        var msgIndex = _messages.IndexOf(msg);
        if (msgIndex < 0) return;

        var newMsg = new ChatMessage(msg.Id, _editText.Trim(), msg.IsUser, msg.Sources) { Timestamp = msg.Timestamp };
        _messages[msgIndex] = newMsg;
        _editText = string.Empty;

        // Remove all messages after this one
        if (msgIndex < _messages.Count - 1)
        {
            _messages.RemoveRange(msgIndex + 1, _messages.Count - msgIndex - 1);
        }

        StateHasChanged();
        await StartStreaming(newMsg.Text);
    }

    private async Task RetryLastMessage()
    {
        _errorMessage = null;

        // Find the last user message
        var lastUserMsg = _messages.LastOrDefault(m => m.IsUser);
        if (lastUserMsg != null)
        {
            await StartStreaming(lastUserMsg.Text);
        }
    }

    private void ClearConversation()
    {
        _messages.Clear();
        _expandedSources.Clear();
        _estimatedTokens = 0;
        _errorMessage = null;
        StateHasChanged();
    }

    private void ToggleSources(string messageId)
    {
        if (_expandedSources.Contains(messageId))
            _expandedSources.Remove(messageId);
        else
            _expandedSources.Add(messageId);
    }

    private void SetQuery(string query)
    {
        _query = query;
        StateHasChanged();
        _inputRef.FocusAsync();
    }

    private async Task ScrollToBottom()
    {
        try
        {
            await JS.InvokeVoidAsync("scrollToBottom", _messageListRef);
        }
        catch
        {
            // Ignore JS errors
        }
    }

    private string RenderMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        // Escape HTML
        text = text.Replace("&", "&amp;")
                   .Replace("<", "&lt;")
                   .Replace(">", "&gt;");

        // Code blocks with language detection
        text = Regex.Replace(text, @"```(\w+)?\n(.*?)```", m =>
        {
            var lang = m.Groups[1].Value;
            var code = m.Groups[2].Value;
            var langClass = string.IsNullOrEmpty(lang) ? "" : $"language-{lang}";
            var displayLang = string.IsNullOrEmpty(lang) ? "code" : lang;

            return $"""
                <div class="code-block">
                    <div class="code-header">
                        <span class="code-lang">{displayLang}</span>
                        <button class="copy-btn" onclick="copyCode(this)" title="Copy code">
                            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path></svg>
                            <span>Copy</span>
                        </button>
                    </div>
                    <pre class="hljs {langClass}"><code>{code}</code></pre>
                </div>
                """;
        }, RegexOptions.Singleline);

        // Inline code
        text = Regex.Replace(text, @"`([^`]+)`", "<code class=\"inline-code\">$1</code>");

        // Headers
        text = Regex.Replace(text, @"^### (.+)$", "<h3>$1</h3>", RegexOptions.Multiline);
        text = Regex.Replace(text, @"^## (.+)$", "<h2>$1</h2>", RegexOptions.Multiline);
        text = Regex.Replace(text, @"^# (.+)$", "<h1>$1</h1>", RegexOptions.Multiline);

        // Bold and italic
        text = Regex.Replace(text, @"\*\*\*(.+?)\*\*\*", "<strong><em>$1</em></strong>");
        text = Regex.Replace(text, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        text = Regex.Replace(text, @"\*(.+?)\*", "<em>$1</em>");
        text = Regex.Replace(text, @"__(.+?)__", "<strong>$1</strong>");
        text = Regex.Replace(text, @"_(.+?)_", "<em>$1</em>");

        // Lists
        text = Regex.Replace(text, @"^\s*[-*]\s+(.+)$", "<li>$1</li>", RegexOptions.Multiline);
        text = Regex.Replace(text, @"(<li>.*</li>)", "<ul>$1</ul>", RegexOptions.Singleline);

        // Numbered lists
        text = Regex.Replace(text, @"^\s*\d+\.\s+(.+)$", "<li>$1</li>", RegexOptions.Multiline);
        text = Regex.Replace(text, @"(<li>.*</li>)", "<ol>$1</ol>", RegexOptions.Singleline);

        // Links
        text = Regex.Replace(text, @"\[([^\]]+)\]\(([^)]+)\)", "<a href=\"$2\" target=\"_blank\" rel=\"noopener\">$1</a>");

        // Blockquotes
        text = Regex.Replace(text, @"^&gt;\s*(.+)$", "<blockquote>$1</blockquote>", RegexOptions.Multiline);

        // Line breaks
        text = text.Replace("\n\n", "</p><p>");
        text = text.Replace("\n", "<br>");

        // Wrap in paragraph if not already wrapped
        if (!text.StartsWith("<") || text.StartsWith("<br>"))
        {
            text = $"<p>{text}</p>";
        }

        return text;
    }

    private string FormatTimestamp(DateTime timestamp)
    {
        var diff = DateTime.Now - timestamp;

        if (diff.TotalMinutes < 1)
            return "just now";
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours}h ago";

        return timestamp.ToString("MMM d");
    }

    private void UpdateTokenCount()
    {
        // Rough estimation: ~4 characters per token
        var totalChars = _messages.Sum(m => m.Text.Length);
        _estimatedTokens = totalChars / 4;
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
