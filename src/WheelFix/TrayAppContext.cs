using System;
using System.Drawing;
using System.Windows.Forms;

namespace WheelFix;

public sealed class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ConfigService _configService = new();
    private readonly StartupService _startupService = new();
    private readonly AppConfig _config;
    private readonly MouseWheelFilter _filter;
    private readonly Timer _statsTimer;

    private readonly ToolStripMenuItem _enabledItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly ToolStripMenuItem _fixCountItem;
    private readonly ToolStripMenuItem _fixPerMinuteItem;
    private readonly ToolStripMenuItem[] _windowItems;

    private bool _isExiting;

    public TrayAppContext()
    {
        _config = _configService.Load();

        var startupEnabled = _startupService.IsEnabled();
        if (_config.StartWithWindows != startupEnabled)
        {
            _config.StartWithWindows = startupEnabled;
        }

        _filter = new MouseWheelFilter(_config.IsFilterEnabled, _config.WindowMilliseconds);
        _filter.FixCountChanged += (_, count) => UpdateFixStatsMenu(count);
        _filter.Start();

        _enabledItem = new ToolStripMenuItem("启用过滤")
        {
            Checked = _config.IsFilterEnabled,
            CheckOnClick = true
        };
        _enabledItem.Click += (_, _) => ToggleFilter();

        _windowItems = BuildWindowMenuItems();

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
        var windowMenu = new ToolStripMenuItem("窗口时间");
        windowMenu.DropDownItems.AddRange(_windowItems);
        menu.Items.Add(_enabledItem);
        menu.Items.Add(windowMenu);
        menu.Items.Add(_fixCountItem);
        menu.Items.Add(_fixPerMinuteItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("退出", null, (_, _) => ExitThread()));

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "WheelFix",
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => ShowStatus();

        _statsTimer = new Timer { Interval = 1000 };
        _statsTimer.Tick += (_, _) => RefreshPerMinuteMenu();
        _statsTimer.Start();

        UpdateTooltip();
        RefreshPerMinuteMenu();
    }

    private ToolStripMenuItem[] BuildWindowMenuItems()
    {
        var items = new ToolStripMenuItem[AppConfig.AllowedWindows.Length];

        for (var i = 0; i < AppConfig.AllowedWindows.Length; i++)
        {
            var ms = AppConfig.AllowedWindows[i];
            var item = new ToolStripMenuItem($"{ms} ms")
            {
                Tag = ms,
                Checked = _config.WindowMilliseconds == ms
            };
            item.Click += (_, _) => SelectWindow(ms);
            items[i] = item;
        }

        return items;
    }

    private void ToggleFilter()
    {
        _config.IsFilterEnabled = _enabledItem.Checked;
        _filter.IsEnabled = _config.IsFilterEnabled;
        UpdateTooltip();
    }

    private void SelectWindow(int ms)
    {
        _config.WindowMilliseconds = ms;
        _filter.WindowMilliseconds = ms;

        foreach (var item in _windowItems)
        {
            item.Checked = (int)item.Tag! == ms;
        }

        UpdateTooltip();
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

    private void UpdateFixStatsMenu(long count)
    {
        if (_fixCountItem.Owner?.InvokeRequired == true)
        {
            _fixCountItem.Owner.Invoke(new Action(() =>
            {
                _fixCountItem.Text = $"修复次数: {count}";
                _fixPerMinuteItem.Text = $"每分钟修复: {_filter.GetFixesInLastMinute()}";
            }));
            return;
        }

        _fixCountItem.Text = $"修复次数: {count}";
        _fixPerMinuteItem.Text = $"每分钟修复: {_filter.GetFixesInLastMinute()}";
    }

    private void RefreshPerMinuteMenu()
    {
        _fixPerMinuteItem.Text = $"每分钟修复: {_filter.GetFixesInLastMinute()}";
    }

    private void ShowStatus()
    {
        var status = $"过滤状态: {(_config.IsFilterEnabled ? "启用" : "禁用")}{Environment.NewLine}" +
                     $"窗口时间: {_config.WindowMilliseconds} ms{Environment.NewLine}" +
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
