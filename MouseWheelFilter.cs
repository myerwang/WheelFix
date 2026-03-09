using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WheelFix;

public sealed class MouseWheelFilter : IDisposable
{
    private const nuint InjectionTag = 0x5748464958000001; // "WHFIX" marker

    private readonly object _lock = new();
    private readonly NativeMethods.LowLevelMouseProc _proc;
    private readonly Queue<DateTime> _fixHistoryUtc = new();

    private nint _hookHandle;
    private bool _disposed;

    private bool _windowActive;
    private int _anchorSign;
    private DateTime _expireAtUtc;

    private bool _isEnabled;
    private int _windowMilliseconds;
    private long _fixCount;

    public MouseWheelFilter(bool isEnabled, int windowMilliseconds)
    {
        _isEnabled = isEnabled;
        _windowMilliseconds = windowMilliseconds;
        _proc = HookCallback;
    }

    public event EventHandler<long>? FixCountChanged;

    public long FixCount => _fixCount;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    public int WindowMilliseconds
    {
        get => _windowMilliseconds;
        set => _windowMilliseconds = value;
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
        if (nCode < 0 || wParam != NativeMethods.WM_MOUSEWHEEL)
        {
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);

        if (data.dwExtraInfo == InjectionTag)
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
            if (!_windowActive || now > _expireAtUtc)
            {
                ActivateWindow(sign, now);
                return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
            }

            if (sign == _anchorSign)
            {
                RefreshWindow(now);
                return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
            }

            var fixedDelta = Math.Abs(delta) * _anchorSign;
            RefreshWindow(now);
            _fixCount++;
            _fixHistoryUtc.Enqueue(now);
            PruneHistory(now);
            FixCountChanged?.Invoke(this, _fixCount);

            InjectWheel(fixedDelta);
            return 1;
        }
    }

    private void ActivateWindow(int anchorSign, DateTime now)
    {
        _windowActive = true;
        _anchorSign = anchorSign;
        _expireAtUtc = now.AddMilliseconds(_windowMilliseconds);
    }

    private void RefreshWindow(DateTime now)
    {
        _expireAtUtc = now.AddMilliseconds(_windowMilliseconds);
    }

    private void PruneHistory(DateTime now)
    {
        while (_fixHistoryUtc.Count > 0 && (now - _fixHistoryUtc.Peek()).TotalSeconds > 60)
        {
            _fixHistoryUtc.Dequeue();
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
            U = new NativeMethods.InputUnion
            {
                mi = new NativeMethods.MOUSEINPUT
                {
                    dwFlags = (uint)NativeMethods.MouseEventF.WHEEL,
                    mouseData = unchecked((uint)delta),
                    dwExtraInfo = InjectionTag
                }
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
