# 平台文档

存放桌面端、移动端的环境、输入、存储、构建、签名和发布约束。

## 文档

- [Android 移植说明](安卓移植说明.md)

## Windows x64 试玩包

Windows 是首发平台。在项目根目录运行 `./scripts/export-windows.sh`，脚本会使用 Release 配置导出独立 PCK 的 Windows x64 版本，并在打包前检查：

- `FangcunCardClub.exe` 和 `FangcunCardClub.pck` 非空。
- 导出目录中只有一份 `data_*_windows_x86_64/` .NET 运行时。
- 运行时包含 Godot C# 程序集、四种玩法程序集以及 `coreclr`、`hostfxr`、`hostpolicy` 等自包含组件。
- ZIP 解压后与本次新导出的临时目录逐文件一致。

最终产物是 Git 忽略的 `artifacts/windows/FangcunCardClub-win64-playtest.zip`；对应解压目录可用于本地结构检查。当前不签名，不上传或发布到外部服务。
