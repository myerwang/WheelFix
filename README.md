# WheelFix

A lightweight Windows tray utility that repairs mouse wheel direction jitter caused by worn mechanical encoders.

---

## English

### What is WheelFix?
WheelFix is a Windows user-mode resident utility (WinForms + .NET 8) that filters accidental opposite wheel events in a short time window.

Typical issue it solves:
- You scroll down, but one unexpected up event appears.
- You scroll up, but one unexpected down event appears.

WheelFix behavior:
- Captures global `WM_MOUSEWHEEL` events via `WH_MOUSE_LL`.
- Opens a short anchor window after a valid scroll direction is observed.
- If opposite direction appears inside that window, WheelFix rewrites it to the anchor direction.
- Original wrong event is swallowed; corrected event is reinjected with `SendInput`.
- Injected events are tagged and ignored by the filter to prevent recursion.

### Features
- System tray resident app (no main window)
- Enable/disable filtering
- Adjustable window: `120 / 150 / 200 / 250 ms`
- Runtime stats:
  - Total fixes in current run
  - Fixes in last minute (rolling 60s)
- Startup toggle via `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- Config persistence to `%LocalAppData%\WheelFix\config.json`

### How to Build & Run
Requirements:
- Windows 10/11
- .NET 8 SDK

Commands:
```bash
dotnet restore
dotnet build
dotnet run
```

### Publish
Framework-dependent:
```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

Self-contained single-file:
```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

### Usage
1. Launch `WheelFix.exe`.
2. Find the tray icon named **WheelFix**.
3. Right-click tray icon:
   - Toggle **Enable Filter**
   - Choose window time
   - View live fix counters
   - Toggle startup
   - Exit
4. Double-click tray icon to show current status.

---

## 中文

### WheelFix 是什么？
WheelFix 是一个 Windows 用户态常驻托盘工具（WinForms + .NET 8），用于修复机械滚轮老化导致的“滚动方向乱跳”问题。

它解决的典型问题：
- 你向下滚动时，偶尔出现一次向上事件。
- 你向上滚动时，偶尔出现一次向下事件。

WheelFix 的处理方式：
- 通过 `WH_MOUSE_LL` 全局钩子捕获 `WM_MOUSEWHEEL`。
- 识别到有效方向后，开启短时间锚定窗口。
- 在窗口内出现反方向事件时，自动修正为锚定方向。
- 吞掉原始错误事件，并用 `SendInput` 注入修正后的事件。
- 对注入事件做标记并忽略，避免递归死循环。

### 功能特性
- 托盘常驻（无主窗口）
- 过滤开关（启用/禁用）
- 窗口时长可选：`120 / 150 / 200 / 250 ms`
- 运行统计：
  - 本次运行修复次数
  - 最近一分钟修复次数（滚动 60 秒）
- 开机启动开关（注册表 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`）
- 配置持久化到 `%LocalAppData%\WheelFix\config.json`

### 构建与运行
环境要求：
- Windows 10/11
- .NET 8 SDK

命令：
```bash
dotnet restore
dotnet build
dotnet run
```

### 发布
依赖运行时版本：
```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

自包含单文件版本：
```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

### 使用说明
1. 启动 `WheelFix.exe`。
2. 在系统托盘找到 **WheelFix** 图标。
3. 右键托盘图标可进行：
   - 启用/禁用过滤
   - 选择窗口时间
   - 查看实时修复统计
   - 开启/关闭开机启动
   - 退出程序
4. 双击托盘图标可查看当前状态。

---

## 日本語

### WheelFix とは？
WheelFix は、劣化した機械式マウスホイールで発生する「逆方向ジャンプ」を補正する Windows ユーザーモード常駐ツール（WinForms + .NET 8）です。

解決する典型的な問題：
- 下にスクロールしたのに、時々上方向イベントが混ざる。
- 上にスクロールしたのに、時々下方向イベントが混ざる。

WheelFix の動作：
- `WH_MOUSE_LL` でグローバル `WM_MOUSEWHEEL` を監視。
- 正常な方向を検出すると短いアンカーウィンドウを開始。
- そのウィンドウ内で逆方向イベントが来たらアンカー方向へ補正。
- 元の誤イベントは破棄し、`SendInput` で補正イベントを再注入。
- 再注入イベントはタグで識別して無視し、再帰を防止。

### 主な機能
- システムトレイ常駐（メイン画面なし）
- フィルター有効/無効の切替
- ウィンドウ時間選択：`120 / 150 / 200 / 250 ms`
- 実行時統計：
  - 今回起動中の補正回数
  - 直近 1 分間の補正回数（60 秒ローリング）
- スタートアップ起動切替（`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`）
- 設定を `%LocalAppData%\WheelFix\config.json` に保存

### ビルドと実行
要件：
- Windows 10/11
- .NET 8 SDK

コマンド：
```bash
dotnet restore
dotnet build
dotnet run
```

### 配布（Publish）
フレームワーク依存版：
```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

自己完結・単一ファイル版：
```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

### 使い方
1. `WheelFix.exe` を起動。
2. システムトレイの **WheelFix** アイコンを確認。
3. アイコンを右クリックして以下を操作：
   - フィルター有効/無効
   - ウィンドウ時間の選択
   - リアルタイム統計の確認
   - スタートアップ起動の切替
   - 終了
4. アイコンをダブルクリックすると現在の状態を表示。
