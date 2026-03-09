# WheelFix

A lightweight Windows tray utility that repairs mouse wheel direction jitter caused by worn mechanical encoders.

![App Logo](app_logo.png)

---

## Visual Previews

### Tray Icons (Status Feedback)
- **Grey (Mouse)**: Idle / Ready
- **Green (Up Arrow)**: Scrolling Up (Direction Locked)
- **Blue (Down Arrow)**: Scrolling Down (Direction Locked)

---

## Project Structure (Engineering Layout)

```text
WheelFix/
  src/
    WheelFix/
      WheelFix.csproj
      Program.cs
      TrayAppContext.cs
      MouseWheelFilter.cs
      NativeMethods.cs
      AppConfig.cs
      ConfigService.cs
      StartupService.cs
  build_output/          # Reserved directory for compiled EXE/packages uploaded from local build
  README.md
```

> Please compile on your local Windows machine, then place generated EXE/publish artifacts into `build_output/`.

---

## English

### What is WheelFix?
WheelFix is a Windows user-mode resident utility (WinForms + .NET 8) that filters accidental opposite wheel events in a short time window.

Typical issue it solves:
- You scroll down, but one unexpected up event appears.
- You scroll up, but one unexpected down event appears.

WheelFix behavior:
- Captures global `WM_MOUSEWHEEL` events via `WH_MOUSE_LL`.
- Opens a timeout window after a valid scroll direction is observed.
- If opposite direction (bounce) appears during the Pause Threshold, WheelFix corrects it to the anchor direction and re-injects it.
- **Triple-Bounce Breakthrough**: If 3 consecutive opposite scroll signals are detected, WheelFix recognizes it as a genuine direction change, instantly unlocks the direction, and **rolls back** any false-positive fix counts.
- **Smart Unblock**: If you physically move your mouse more than 30 pixels, the timeout lock is instantly cancelled.
- Ensures exact scroll distance consistency and a seamless user experience.
- Tray icon reflects the current locked direction and stays visible during the Pause Threshold.

### Features
- System tray resident app (no main window)
- Enable/disable filtering
- Pause suppression timeout: `关闭 / 0.5s / 1s / 2s / 3s / 5s / 10s` (corrects bounces after scroll stops)
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
cd src/WheelFix
dotnet restore
dotnet build
dotnet run
```

### Publish
Framework-dependent:
```bash
cd src/WheelFix
dotnet publish -c Release -r win-x64 --self-contained false -o ../../build_output/framework-dependent
```

Self-contained single-file:
```bash
cd src/WheelFix
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o ../../build_output/self-contained-single-file
```

### Usage
1. Launch `WheelFix.exe`.
2. Find the tray icon named **WheelFix**.
3. Right-click tray icon:
   - Toggle **Enable Filter**
   - Choose pause suppression threshold
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
- 识别到有效方向后，开启锚定防抖计时。
- 在停顿抑制期内出现反方向事件（回弹）时，自动将其修正为锚定方向并重新注入。
- 确保滚动的绝对连贯性，将错误反弹瞬间转化为顺手滚动。
- **三连击穿透 (Triple-Bounce Breakthrough)**：如果程序连续检测到 3 个反方向信号，它会智能判定这是真实的变向操作而非杂讯。程序会即刻解除方向锁定顺应新方向，并**自动撤销（Rollback）**前两次误判的修复记录。
- **智能断桥 (Smart Unblock)**：在抑制期间，如果您实体滑动了鼠标（屏幕光标位移>30像素），程序会立即解除时间锁定，不影响您正常切换方向。
- 托盘图标实时反馈当前锁定方向，并在停顿抑制期内保持显示。

### 功能特性
- 托盘常驻（无主窗口）
- 过滤开关（启用/禁用）
- 停顿抑制设定：`关闭 / 0.5s / 1s / 2s / 3s / 5s / 10s`（处理滚动停止后再启动时的乱跳回弹）
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
cd src/WheelFix
dotnet restore
dotnet build
dotnet run
```

### 发布
依赖运行时版本：
```bash
cd src/WheelFix
dotnet publish -c Release -r win-x64 --self-contained false -o ../../build_output/framework-dependent
```

自包含单文件版本：
```bash
cd src/WheelFix
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o ../../build_output/self-contained-single-file
```

### 使用说明
1. 启动 `WheelFix.exe`。
2. 在系统托盘找到 **WheelFix** 图标。
3. 右键托盘图标可进行：
   - 启用/禁用过滤
   - 选择停顿抑制设定
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
- **トリプルバウンス突破 (Triple-Bounce Breakthrough)**: 逆方向の信号が連続3回検出された場合、「意図的な方向転換」と判断して即座にロックを解除。さらに誤って記録された最初の2回の補正カウントを自動的に取り消し (Rollback) ます。
- **スマートアンロック (Smart Unblock)**: マウスカーソルを物理的に動かした場合 (30ピクセル以上)、アンカーウィンドゥが即座に解除され、方向転換がスムーズに行えます。
- 元の誤イベントは破棄し、`SendInput` で補正イベントを再注入します。
- 再注入イベントは自身のタグおよびフラグで識別・除外されるため、再帰を完全に防止します。

### 主な機能
- システムトレイ常駐（メイン画面なし）
- フィルター有効/無効の切替
- 抑制時間選択：`关闭 / 0.5s / 1s / 2s / 3s / 5s / 10s`
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
cd src/WheelFix
dotnet restore
dotnet build
dotnet run
```

### 配布（Publish）
フレームワーク依存版：
```bash
cd src/WheelFix
dotnet publish -c Release -r win-x64 --self-contained false -o ../../build_output/framework-dependent
```

自己完結・単一ファイル版：
```bash
cd src/WheelFix
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o ../../build_output/self-contained-single-file
```

### 使い方
1. `WheelFix.exe` を起動。
2. システムトレイの **WheelFix** アイコンを確認。
3. アイコンを右クリックして以下を操作：
   - フィルター有効/無効
   - 抑制時間の選択
   - リアルタイム統計の確認
   - スタートアップ起動の切替
   - 終了
4. アイコンをダブルクリックすると現在の状態を表示。
