# 增量记账

一个本地运行的 Windows 记账工具，用于记录每个人逐月增长的累计金额，并自动计算本期新增金额。

[English README](README_EN.md)

![增量记账主界面](docs/screenshot.jpg)

## 功能

- 按月份管理账期，并可选择承接上一账期的累计金额。
- 直接编辑本期累计金额，自动计算本期新增金额。
- 支持搜索、状态筛选、仅查看有变化的人员，以及金额清单预览。
- 支持批量标记已报销/待报销，复制或导出 TXT 清单。
- 支持导入 TXT（`姓名：金额`），可选择导入累计金额或本次新增金额。
- 自动保存、每日备份、完整 JSON 备份与恢复。

## 首次运行

首次运行会在 `%LOCALAPPDATA%\LevelUpAccrue\ledger.json` 创建当前月份的空账期，不会再写入示例人员或示例金额。打开后可直接点击“添加人员”，或导入自己的 TXT 账单。

从旧版本升级时，如果检测到软件曾写入的内置示例账本，程序会先将其保存到 `Backups\内置示例数据迁移前_*.json`，然后切换为空账本。其他已有账本数据不会被清除。

## 数据与备份

- 主数据：`%LOCALAPPDATA%\LevelUpAccrue\ledger.json`
- 自动备份：`%LOCALAPPDATA%\LevelUpAccrue\Backups\`
- 也可以使用顶部“备份”和“恢复”按钮手动管理完整 JSON 账本。
- 删除人员只会从当前及后续账期移除，历史账期仍会保留。

## 快捷键

- `Ctrl+N`：新建账期
- `Ctrl+I`：导入 TXT
- `Ctrl+F`：搜索人员
- `Ctrl+B`：导出完整备份
- `Ctrl+S`：立即保存

## 开发

需要 Windows 和 .NET 8 SDK。

```powershell
dotnet build LevelUpAccrue.slnx -c Release
dotnet run --project src/LevelUpAccrue/LevelUpAccrue.csproj
dotnet run --project tests/LevelUpAccrue.SmokeTests -c Release
```

生成自包含的 Windows 单文件发布包：

```powershell
dotnet publish src/LevelUpAccrue/LevelUpAccrue.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -o publish/win-x64
```

## 许可

本项目以 [MIT License](LICENSE) 发布。
