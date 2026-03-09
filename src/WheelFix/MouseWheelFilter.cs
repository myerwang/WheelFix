using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WheelFix;

public sealed class MouseWheelFilter : IDisposable
{
    private static readonly nuint InjectionTag = unchecked((nuint)0x5748464958000001); // "WHFIX" marker

    private readonly object _lock = new();
    private readonly NativeMethods.LowLevelMouseProc _proc;
    private readonly List<DateTime> _fixHistoryUtc = new();

    public event Action<int>? StateChanged; // 0: Idle, -1: Up, 1: Down

    private nint _hookHandle;
    private bool _disposed;

    private bool _isEnabled;
    private int _pauseThresholdMilliseconds;
    private int _fixCount;

    private int _anchorSign;
    private DateTime _lastEventUtc = DateTime.MinValue;
    private int _reverseScore;
    private int _pendingFixesForRollback;
    private NativeMethods.POINT _lastWheelPt;
    private const int MoveThresholdSq = 900; // 30 pixels squared

    public MouseWheelFilter(bool isEnabled, int pauseThresholdMilliseconds)
    {
        _isEnabled = isEnabled;
        _pauseThresholdMilliseconds = pauseThresholdMilliseconds;
        _proc = HookCallback;
    }

    // Removed: public event EventHandler<long>? FixCountChanged;

    public int FixCount => _fixCount;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    public int PauseThresholdMilliseconds
    {
        get => _pauseThresholdMilliseconds;
        set => _pauseThresholdMilliseconds = value;
    }

    public int GetCurrentState() // 1: Up, 0: Idle, -1: Down
    {
        lock (_lock)
        {
            if (_anchorSign == 0) return 0;
            var elapsed = (DateTime.UtcNow - _lastEventUtc).TotalMilliseconds;
            if (elapsed < _pauseThresholdMilliseconds)
            {
                return _anchorSign;
            }
            return 0;
        }
    }

    public int GetFixesInLastMinute()
    {
        lock (_lock)
        {
            PruneHistory(DateTime.UtcNow);
            return _fixHistoryUtc.Count;
        }
    }

    public void Start()
    {
        if (_hookHandle != nint.Zero)
        {
            return;
        }

        _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _proc, nint.Zero, 0);
        if (_hookHandle == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to install mouse hook.");
        }
    }

    public void Stop()
    {
        if (_hookHandle == nint.Zero)
        {
            return;
        }

        if (!NativeMethods.UnhookWindowsHookEx(_hookHandle))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to uninstall mouse hook.");
        }

        _hookHandle = nint.Zero;
    }

    private nint HookCallback(int nCode, nuint wParam, nint lParam)
    {
        if (nCode < 0)
        {
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        if (wParam != NativeMethods.WM_MOUSEWHEEL && wParam != NativeMethods.WM_MOUSEMOVE)
        {
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);

        if (wParam == NativeMethods.WM_MOUSEMOVE)
        {
            if (_isEnabled && _pauseThresholdMilliseconds > 0 && _anchorSign != 0)
            {
                var dx = data.pt.X - _lastWheelPt.X;
                var dy = data.pt.Y - _lastWheelPt.Y;
                if (dx * dx + dy * dy > MoveThresholdSq)
                {
                    lock (_lock)
                    {
                        if (_anchorSign != 0)
                        {
                            _anchorSign = 0;
                            _lastEventUtc = DateTime.MinValue; // reset threshold
                            _reverseScore = 0;
                            _pendingFixesForRollback = 0;
                            System.Threading.Tasks.Task.Run(() => StateChanged?.Invoke(0));
                        }
                    }
                }
            }
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        if (data.dwExtraInfo == InjectionTag || (data.flags & 1) != 0)
        {
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        var delta = GetWheelDelta(data.mouseData);
        if (delta == 0 || !_isEnabled)
        {
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        var sign = Math.Sign(delta);
        var now = DateTime.UtcNow;

        lock (_lock)
        {
            _lastWheelPt = data.pt; // Refresh anchor point

            var elapsed = (now - _lastEventUtc).TotalMilliseconds;

            // Scenario 1: First scroll or threshold has naturally expired
            if (_anchorSign == 0 || elapsed >= _pauseThresholdMilliseconds)
            {
                var originalAnchor = _anchorSign;
                _anchorSign = sign;
                _lastEventUtc = now;
                _reverseScore = 0;
                _pendingFixesForRollback = 0;

                if (originalAnchor != _anchorSign)
                {
                    var state = _anchorSign;
                    System.Threading.Tasks.Task.Run(() => StateChanged?.Invoke(state));
                }
                return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
            }

            // Scenario 2: Within threshold, matches current anchor direction
            if (sign == _anchorSign)
            {
                var ticks = Math.Max(1, Math.Abs(delta) / 120);
                _lastEventUtc = now;
                if (_reverseScore > 0)
                {
                    _reverseScore -= ticks;
                    if (_reverseScore <= 0)
                    {
                        _reverseScore = 0;
                        // Reverse attempt string defeated. Those previous intercepts were indeed just bounces.
                        _pendingFixesForRollback = 0;
                    }
                }
                return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
            }
            else
            {
                // Scenario 3: Opposite direction tick (potential bounce or real attempt to change direction)
                var ticks = Math.Max(1, Math.Abs(delta) / 120);
                _reverseScore += ticks;

                // If net opposite signals reach 3, it's a genuine direction change!
                if (_reverseScore >= 3)
                {
                    _anchorSign = sign;
                    _lastEventUtc = now;
                    _reverseScore = 0;

                    // Rollback false-positive fixes tentatively added during the struggle
                    if (_fixCount >= _pendingFixesForRollback) 
                        _fixCount -= _pendingFixesForRollback;
                    
                    for (int i = 0; i < _pendingFixesForRollback && _fixHistoryUtc.Count > 0; i++)
                    {
                        _fixHistoryUtc.RemoveAt(_fixHistoryUtc.Count - 1);
                    }
                    _pendingFixesForRollback = 0;

                    var state = _anchorSign;
                    System.Threading.Tasks.Task.Run(() => StateChanged?.Invoke(state));
                    return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
                }

                // It's still considered a mechanical bounce or suppressed input.
                // Intercept and correct it!
                var fixedDelta = Math.Abs(delta) * _anchorSign;
                _lastEventUtc = now; // Update time to extend the anchor continuity
                
                _fixCount++;
                _fixHistoryUtc.Add(now);
                PruneHistory(now);

                _pendingFixesForRollback++;

                System.Threading.Tasks.Task.Run(() => InjectWheel(fixedDelta));
                return 1;
            }
        }
    }

    private void PruneHistory(DateTime now)
    {
        while (_fixHistoryUtc.Count > 0 && (now - _fixHistoryUtc[0]).TotalSeconds > 60)
        {
            _fixHistoryUtc.RemoveAt(0);
        }
    }

    private static int GetWheelDelta(uint mouseData)
    {
        var highWord = (short)((mouseData >> 16) & 0xFFFF);
        return highWord;
    }

    private static void InjectWheel(int delta)
    {
        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            mi = new NativeMethods.MOUSEINPUT
            {
                dwFlags = (uint)NativeMethods.MouseEventF.WHEEL,
                mouseData = unchecked((uint)delta),
                dwExtraInfo = InjectionTag
            }
        };

        var sent = NativeMethods.SendInput(1, [input], Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SendInput failed for repaired wheel event.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            Stop();
        }
        catch
        {
            // no-op during dispose path
        }

        _disposed = true;
    }
}
