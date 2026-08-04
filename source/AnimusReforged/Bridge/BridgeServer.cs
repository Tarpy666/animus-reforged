using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace AnimusReforged.Bridge;

/// <summary>
/// Local WebSocket bridge between the l1l-@p3p overlay (browser / transparent window)
/// and the Animus Reforged launcher. Zero external dependencies — pure BCL
/// (<see cref="HttpListener"/> + <see cref="WebSocket"/>).
///
/// Protocol (JSON messages, one object per frame):
///   overlay → launcher:  { "type": "overlay.chat", "text": "..." }
///                         { "type": "overlay.cmd",  "cmd": "screenshot" }
///   launcher → overlay:  { "type": "game.state", "running": true, "process": "AssassinsCreed_Dx9" }
///                         { "type": "ai.say",      "text": "..." }
///                         { "type": "pong",        "at": 123456 }
/// </summary>
public sealed class BridgeServer : IDisposable
{
    /// <summary>Default local port. Bind to 127.0.0.1 so nothing is exposed to the network.</summary>
    public const int DefaultPort = 4747;

    /// <summary>Singleton instance, set by <see cref="StartDefault"/>.</summary>
    public static BridgeServer? Instance { get; private set; }

    private readonly HttpListener _listener = new();
    private readonly object _sync = new();
    private readonly List<WebSocket> _clients = new();
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;

    /// <summary>Port the bridge listens on.</summary>
    public int Port { get; }

    /// <summary>True once <see cref="Start"/> succeeds.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>Number of currently connected overlay clients.</summary>
    public int ClientCount
    {
        get { lock (_sync) { return _clients.Count; } }
    }

    private BridgeServer(int port)
    {
        Port = port;
    }

    /// <summary>
    /// Starts the singleton bridge on <see cref="DefaultPort"/>. Safe to call
    /// multiple times — a second call returns the existing instance.
    /// </summary>
    public static BridgeServer StartDefault(int port = DefaultPort)
    {
        if (Instance is { IsRunning: true })
        {
            return Instance;
        }

        Instance = new BridgeServer(port);
        Instance.Start();
        return Instance;
    }

    /// <summary>Begins listening and accepting overlay connections.</summary>
    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        try
        {
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            _listener.Start();
            IsRunning = true;
            _cts = new CancellationTokenSource();
            _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
            Logging.Logger.Info<BridgeServer>($"Bridge listening on ws://127.0.0.1:{Port}/");
        }
        catch (Exception ex)
        {
            IsRunning = false;
            Logging.Logger.Error<BridgeServer>($"Bridge failed to start: {ex.Message}");
        }
    }

    /// <summary>Stops the bridge and disposes all connected sockets.</summary>
    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;
        _cts?.Cancel();

        try { _listener.Stop(); }
        catch { /* already closed */ }

        lock (_sync)
        {
            foreach (WebSocket ws in _clients)
            {
                try { ws.Dispose(); } catch { /* best effort */ }
            }
            _clients.Clear();
        }

        try { _acceptLoop?.Wait(TimeSpan.FromSeconds(2)); }
        catch { /* task cancelled */ }

        _cts?.Dispose();
        Logging.Logger.Info<BridgeServer>("Bridge stopped");
    }

    /// <summary>Reports game process state to every connected overlay.</summary>
    public static void ReportGameState(bool running, string? process = null)
    {
        Instance?.Broadcast(new
        {
            type = "game.state",
            running,
            process,
            at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }

    /// <summary>Pushes an AI message to the overlay (speech bubble / HUD).</summary>
    public static void Say(string text)
    {
        Instance?.Broadcast(new { type = "ai.say", text });
    }

    /// <summary>Sends a raw JSON object to every connected client.</summary>
    public void Broadcast(object message)
    {
        string json;
        try { json = JsonSerializer.Serialize(message); }
        catch (Exception ex)
        {
            Logging.Logger.Error<BridgeServer>($"Broadcast serialize failed: {ex.Message}");
            return;
        }

        byte[] payload = Encoding.UTF8.GetBytes(json);
        lock (_sync)
        {
            foreach (WebSocket ws in _clients.ToArray())
            {
                try
                {
                    if (ws.State == WebSocketState.Open)
                    {
                        ws.SendAsync(new ArraySegment<byte>(payload),
                            WebSocketMessageType.Text, true, CancellationToken.None)
                            .GetAwaiter().GetResult();
                    }
                }
                catch
                {
                    _clients.Remove(ws);
                }
            }
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener.IsListening)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync().WaitAsync(ct);
            }
            catch
            {
                break; // listener stopped or cancelled
            }

            if (ctx.Request.IsWebSocketRequest)
            {
                _ = Task.Run(() => HandleWebSocketAsync(ctx, ct));
            }
            else
            {
                // Tiny status endpoint for plain HTTP probes (e.g. health check in a browser tab)
                byte[] body = Encoding.UTF8.GetBytes($"{{\"bridge\":true,\"port\":{Port},\"clients\":{ClientCount}}}");
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json";
                ctx.Response.ContentLength64 = body.Length;
                await ctx.Response.OutputStream.WriteAsync(body, ct);
                ctx.Response.Close();
            }
        }
    }

    private async Task HandleWebSocketAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        WebSocket ws;
        try
        {
            HttpListenerWebSocketContext wsc = await ctx.AcceptWebSocketAsync(null);
            ws = wsc.WebSocket;
        }
        catch (Exception ex)
        {
            Logging.Logger.Error<BridgeServer>($"WebSocket accept failed: {ex.Message}");
            return;
        }

        lock (_sync)
        {
            _clients.Add(ws);
        }
        Logging.Logger.Info<BridgeServer>($"Overlay connected ({ClientCount} total)");

        try
        {
            byte[] buffer = new byte[8192];
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                WebSocketReceiveResult result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text && result.Count > 0)
                {
                    string text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    HandleMessage(text);
                }
            }
        }
        catch (Exception ex)
        {
            Logging.Logger.Debug<BridgeServer>($"Overlay disconnected: {ex.Message}");
        }
        finally
        {
            lock (_sync)
            {
                _clients.Remove(ws);
            }
            try { ws.Dispose(); } catch { /* best effort */ }
            Logging.Logger.Info<BridgeServer>($"Overlay disconnected ({ClientCount} total)");
        }
    }

    /// <summary>Routes an incoming overlay message to the right handler.</summary>
    private void HandleMessage(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("type", out JsonElement typeEl))
            {
                return;
            }

            string type = typeEl.GetString() ?? string.Empty;
            switch (type)
            {
                case "overlay.chat":
                    string? text = root.TryGetProperty("text", out JsonElement t) ? t.GetString() : null;
                    Logging.Logger.Info<BridgeServer>($"[overlay] {text}");
                    OnOverlayChat?.Invoke(text ?? string.Empty);
                    break;

                case "overlay.cmd":
                    string? cmd = root.TryGetProperty("cmd", out JsonElement c) ? c.GetString() : null;
                    Logging.Logger.Info<BridgeServer>($"[overlay cmd] {cmd}");
                    OnOverlayCommand?.Invoke(cmd ?? string.Empty);
                    break;

                case "ping":
                    Broadcast(new { type = "pong", at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
                    break;
            }
        }
        catch (Exception ex)
        {
            Logging.Logger.Error<BridgeServer>($"Bad overlay message: {ex.Message}");
        }
    }

    /// <summary>Raised when the overlay sends a chat line. Wire this to the AI backend.</summary>
    public event Action<string>? OnOverlayChat;

    /// <summary>Raised when the overlay sends a command (e.g. "screenshot").</summary>
    public event Action<string>? OnOverlayCommand;

    public void Dispose()
    {
        Stop();
        _listener.Close();
    }
}
