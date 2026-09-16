using System.Runtime.InteropServices;
using Mixline.Core;
using Mixline.Logging;

namespace Mixline.IPC;

public sealed class HotkeyService : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const int WmHotkey = 0x0312;

    private readonly AppLog _log;
    private readonly Dictionary<int, HotkeyBinding> _map = new();
    private IntPtr _hwnd;
    private int _next = 1;

    public event Action<string>? Triggered;
    public event Action<HotkeyConflict>? Conflict;

    public HotkeyService(AppLog log)
    {
        _log = log;
    }

    public void Attach(IntPtr hwnd) => _hwnd = hwnd;

    public void Clear()
    {
        foreach (var id in _map.Keys.ToArray())
            UnregisterHotKey(_hwnd, id);
        _map.Clear();
    }

    public Result Register(HotkeyBinding binding)
    {
        if (_hwnd == IntPtr.Zero)
            return Result.Fail("Hotkeys are not ready yet.");

        foreach (var existing in _map.Values)
        {
            if (existing.Modifiers == binding.Modifiers && existing.Key == binding.Key && existing.Id != binding.Id)
            {
                var conflict = new HotkeyConflict
                {
                    Id = binding.Id,
                    Display = binding.Display,
                    Message = $"Shortcut {binding.Display} is already used by {existing.Id}."
                };
                Conflict?.Invoke(conflict);
                return Result.Fail(conflict.Message);
            }
        }

        var atom = _next++;
        var mods = binding.Modifiers | HotkeyParser.ModNoRepeat;
        if (!RegisterHotKey(_hwnd, atom, mods, binding.Key))
        {
            var err = Marshal.GetLastWin32Error();
            var message = err == 1409
                ? $"Shortcut {binding.Display} is already registered by Windows or another app."
                : $"Could not register {binding.Display}.";
            Conflict?.Invoke(new HotkeyConflict { Id = binding.Id, Display = binding.Display, Message = message });
            _log.Warning("hotkeys", message, $"win32={err}");
            return Result.Fail(message);
        }

        _map[atom] = binding;
        _log.Info("hotkeys", $"Registered {binding.Display} for {binding.Id}");
        return Result.Ok();
    }

    public bool Handle(int msg, IntPtr wParam)
    {
        if (msg != WmHotkey)
            return false;
        var id = wParam.ToInt32();
        if (_map.TryGetValue(id, out var binding))
        {
            Triggered?.Invoke(binding.Id);
            return true;
        }
        return false;
    }

    public void Dispose() => Clear();
}
