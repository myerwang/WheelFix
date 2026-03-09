---
description: 编译 WheelFix 为自包含单文件的可执行文件
---
1. 切换到工作目录
2. 进入源代码所在的 `src/WheelFix` 目录
3. 执行发布命令，生成目标产物到 `build_output`

// turbo-all
```bash
cd c:\Task\000Project\WheelFix\src\WheelFix
Remove-Item -Recurse -Force bin -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force obj -ErrorAction SilentlyContinue
& "C:\Program Files\dotnet\dotnet.exe" publish -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true /p:AssemblyName=WheelFix -o ../../build_output
```
