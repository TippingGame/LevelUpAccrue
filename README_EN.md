# Increment Ledger

A local Windows desktop app for tracking each person's cumulative amount by month and calculating the increase for the current period.

[中文 README](README.md)

![Increment Ledger main window](docs/screenshot.jpg)

## Features

- Manage monthly periods and optionally carry cumulative amounts forward.
- Edit current cumulative amounts directly; the current-period increase is calculated automatically.
- Search and filter by reimbursement status or changed entries, with a text preview of selected amounts.
- Mark selected people as reimbursed or pending in bulk, then copy or export a TXT list.
- Import TXT files in `Name: Amount` format as either cumulative amounts or new increments.
- Automatic saves, daily backups, and full JSON backup/restore.

## First Run

On first launch, the app creates an empty period for the current month at `%LOCALAPPDATA%\LevelUpAccrue\ledger.json`. It no longer inserts sample people or sample amounts. You can add people immediately or import your own TXT ledger.

When upgrading from an older build, the app recognizes the built-in sample ledger that older builds created, saves it to `Backups\内置示例数据迁移前_*.json`, and then switches to an empty ledger. Other existing user data is left untouched.

## Data and Backups

- Main data: `%LOCALAPPDATA%\LevelUpAccrue\ledger.json`
- Automatic backups: `%LOCALAPPDATA%\LevelUpAccrue\Backups\`
- Use the **Backup** and **Restore** buttons to manage complete JSON ledger files manually.
- Deactivating a person removes them from the current and future periods while preserving historical periods.

## Keyboard Shortcuts

- `Ctrl+N`: New period
- `Ctrl+I`: Import TXT
- `Ctrl+F`: Focus search
- `Ctrl+B`: Export full backup
- `Ctrl+S`: Save now

## Development

Requires Windows and the .NET 8 SDK.

```powershell
dotnet build LevelUpAccrue.slnx -c Release
dotnet run --project src/LevelUpAccrue/LevelUpAccrue.csproj
dotnet run --project tests/LevelUpAccrue.SmokeTests -c Release
```

Create a self-contained single-file Windows build:

```powershell
dotnet publish src/LevelUpAccrue/LevelUpAccrue.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -o publish/win-x64
```

## License

Released under the [MIT License](LICENSE).
