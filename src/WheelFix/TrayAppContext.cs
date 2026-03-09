using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace WheelFix;

public sealed class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ConfigService _configService = new();
    private readonly StartupService _startupService = new();
    private readonly AppConfig _config;
    private readonly MouseWheelFilter _filter;
    private readonly System.Windows.Forms.Timer _statsTimer;

    private readonly ToolStripMenuItem _enabledItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly ToolStripMenuItem _fixCountItem;
    private readonly ToolStripMenuItem _fixPerMinuteItem;
    private readonly ToolStripMenuItem[] _pauseItems;

    private bool _isExiting;
    private int _currentState = 0; // -1: Up, 0: Idle, 1: Down

    public TrayAppContext()
    {
        _config = _configService.Load();

        var startupEnabled = _startupService.IsEnabled();
        if (_config.StartWithWindows != startupEnabled)
        {
            _config.StartWithWindows = startupEnabled;
        }

        _filter = new MouseWheelFilter(_config.IsFilterEnabled, _config.PauseThresholdMilliseconds);
        _filter.StateChanged += state => UpdateIcon(state);
        _filter.Start();

        _enabledItem = new ToolStripMenuItem("启用过滤")
        {
            Checked = _config.IsFilterEnabled,
            CheckOnClick = true
        };
        _enabledItem.Click += (_, _) => ToggleFilter();

        _pauseItems = BuildPauseMenuItems();

        _fixCountItem = new ToolStripMenuItem("修复次数: 0")
        {
            Enabled = false
        };

        _fixPerMinuteItem = new ToolStripMenuItem("每分钟修复: 0")
        {
            Enabled = false
        };

        _startupItem = new ToolStripMenuItem("开机启动")
        {
            Checked = _config.StartWithWindows,
            CheckOnClick = true
        };
        _startupItem.Click += (_, _) => ToggleStartup();

        var menu = new ContextMenuStrip();
        var pauseMenu = new ToolStripMenuItem("停顿抑制");
        pauseMenu.DropDownItems.AddRange(_pauseItems);

        menu.Items.Add(_enabledItem);
        menu.Items.Add(pauseMenu);
        menu.Items.Add(_fixCountItem);
        menu.Items.Add(_fixPerMinuteItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("退出", null, (_, _) => ExitThread()));

        _notifyIcon = new NotifyIcon
        {
            Icon = GetIconForState(0),
            Text = "WheelFix",
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => ShowStatus();

        _statsTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _statsTimer.Tick += (_, _) => RefreshStatsMenu();
        _statsTimer.Start();

        UpdateTooltip();
        RefreshStatsMenu();
    }

    private ToolStripMenuItem[] BuildPauseMenuItems()
    {
        var items = new ToolStripMenuItem[AppConfig.AllowedPauseThresholds.Length];

        for (var i = 0; i < AppConfig.AllowedPauseThresholds.Length; i++)
        {
            var ms = AppConfig.AllowedPauseThresholds[i];
            var label = ms == 0 ? "关闭" : $"{ms / 1000.0} s";
            var item = new ToolStripMenuItem(label)
            {
                Tag = ms,
                Checked = _config.PauseThresholdMilliseconds == ms
            };
            item.Click += (_, _) => SelectPauseThreshold(ms);
            items[i] = item;
        }

        return items;
    }

    private void ToggleFilter()
    {
        _config.IsFilterEnabled = _enabledItem.Checked;
        _filter.IsEnabled = _config.IsFilterEnabled;
        UpdateIcon(_currentState);
        UpdateTooltip();
    }

    private void SelectPauseThreshold(int ms)
    {
        _config.PauseThresholdMilliseconds = ms;
        _filter.PauseThresholdMilliseconds = ms;

        foreach (var item in _pauseItems)
        {
            item.Checked = (int)item.Tag! == ms;
        }
    }

    private void ToggleStartup()
    {
        try
        {
            _startupService.SetEnabled(_startupItem.Checked);
            _config.StartWithWindows = _startupItem.Checked;
        }
        catch (Exception ex)
        {
            _startupItem.Checked = !_startupItem.Checked;
            MessageBox.Show($"设置开机启动失败：{ex.Message}", "WheelFix", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshStatsMenu()
    {
        // Periodic check to reset icon if pause threshold has passed
        UpdateIcon(_filter.GetCurrentState());
        
        _fixCountItem.Text = $"修复次数: {_filter.FixCount}";
        _fixPerMinuteItem.Text = $"每分钟修复: {_filter.GetFixesInLastMinute()}";
    }

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    internal static extern bool DestroyIcon(IntPtr hIcon);

    private void UpdateIcon(int state)
    {
        if (_currentState == state && _notifyIcon.Icon != null) return;
        
        var oldIcon = _notifyIcon.Icon;
        _notifyIcon.Icon = GetIconForState(state);
        
        if (oldIcon != null && oldIcon != SystemIcons.Application)
        {
            DestroyIcon(oldIcon.Handle);
            oldIcon.Dispose();
        }
    }

    private Icon GetIconForState(int state)
    {
        if (!_config.IsFilterEnabled) state = 0;
        _currentState = state;

        using var bitmap = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // Background
        var bgColor = state == 0 ? Color.FromArgb(64, 64, 64) : (state == 1 ? Color.ForestGreen : Color.DodgerBlue);
        using var brush = new SolidBrush(bgColor);
        g.FillEllipse(brush, 2, 2, 28, 28);

        // Foreground
        using var pen = new Pen(Color.White, 3);
        if (state == 0)
        {
            // Draw Minimalist Mouse
            g.DrawEllipse(pen, 8, 4, 16, 24); // Mouse body
            g.DrawLine(pen, 16, 4, 16, 12);   // Button split

            // Broken wheel (red X)
            using var redPen = new Pen(Color.Tomato, 2);
            g.DrawLine(redPen, 14, 10, 18, 14);
            g.DrawLine(redPen, 18, 10, 14, 14);
        }
        else if (state == 1)
        {
            // Up Arrow
            g.DrawLine(pen, 16, 8, 16, 24);
            g.DrawLine(pen, 16, 8, 8, 16);
            g.DrawLine(pen, 16, 8, 24, 16);
        }
        else if (state == -1)
        {
            // Down Arrow
            g.DrawLine(pen, 16, 8, 16, 24);
            g.DrawLine(pen, 16, 24, 8, 16);
            g.DrawLine(pen, 16, 24, 24, 16);
        }

        return Icon.FromHandle(bitmap.GetHicon());
    }

    private void ShowStatus()
    {
        var pauseText = _config.PauseThresholdMilliseconds == 0 ? "已关闭" : $"{_config.PauseThresholdMilliseconds / 1000.0} s";
        var status = $"过滤状态: {(_config.IsFilterEnabled ? "启用" : "禁用")}{Environment.NewLine}" +
                     $"停顿抑制: {pauseText}{Environment.NewLine}" +
                     $"本次运行修复次数: {_filter.FixCount}{Environment.NewLine}" +
                     $"每分钟修复次数: {_filter.GetFixesInLastMinute()}{Environment.NewLine}" +
                     $"开机启动: {(_config.StartWithWindows ? "开启" : "关闭")}";

        MessageBox.Show(status, "WheelFix 状态", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void UpdateTooltip()
    {
        var state = _config.IsFilterEnabled ? "已启用" : "已禁用";
        _notifyIcon.Text = $"WheelFix - {state}";
    }

    protected override void ExitThreadCore()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;

        try
        {
            _config.TotalFixCount += _filter.FixCount;
            _configService.Save(_config);
        }
        catch
        {
            // ignore save errors during shutdown
        }

        _statsTimer.Stop();
        _statsTimer.Dispose();
        _notifyIcon.Visible = false;
        _filter.Dispose();
        _notifyIcon.Dispose();

        base.ExitThreadCore();
    }
}
