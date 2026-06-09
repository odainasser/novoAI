using Microsoft.JSInterop;

namespace Web.Offline;

// Bridges window 'online' / 'offline' events into managed code. Scoped per-circuit,
// so each component that wires the layout subscribes for its own lifetime.
//
// Usage: inject, then call AttachAsync(onOnline, onOffline). Dispose detaches.
public class OfflineNetworkMonitor : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private DotNetObjectReference<NetworkCallbacks>? _ref;
    private string? _token;

    public OfflineNetworkMonitor(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<bool> IsOnlineAsync()
    {
        try
        {
            return await _js.InvokeAsync<bool>("CashierNetwork.isOnline");
        }
        catch
        {
            // If we can't reach JS we're effectively offline (or pre-render).
            return true;
        }
    }

    public async Task AttachAsync(Func<Task> onOnline, Func<Task> onOffline)
    {
        if (_ref is not null) return;

        var callbacks = new NetworkCallbacks(onOnline, onOffline);
        _ref = DotNetObjectReference.Create(callbacks);
        _token = Guid.NewGuid().ToString("N");

        try
        {
            await _js.InvokeVoidAsync("CashierNetwork.attach", _token, _ref);
        }
        catch
        {
            _ref?.Dispose();
            _ref = null;
            _token = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_ref is null) return;
        try
        {
            if (_token is not null)
                await _js.InvokeVoidAsync("CashierNetwork.detach", _token);
        }
        catch { /* page is unloading — best effort */ }
        _ref?.Dispose();
        _ref = null;
        _token = null;
    }

    // The class JSRuntime invokes via DotNetObjectReference. Public methods are
    // required (and decorated with [JSInvokable]) so Blazor's serializer can find them.
    public sealed class NetworkCallbacks
    {
        private readonly Func<Task> _onOnline;
        private readonly Func<Task> _onOffline;

        public NetworkCallbacks(Func<Task> onOnline, Func<Task> onOffline)
        {
            _onOnline = onOnline;
            _onOffline = onOffline;
        }

        [JSInvokable]
        public Task OnOnline() => _onOnline();

        [JSInvokable]
        public Task OnOffline() => _onOffline();
    }
}
