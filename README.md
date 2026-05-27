# QR-deFuzzer

QR-deFuzzer is a lightweight Windows tray app for finding and decoding QR codes on screen. It is aimed at QR codes that appear in browsers, VPN portals, admin dashboards, password-manager setup flows, and 2FA enrolment pages.

The app can scan all connected monitors automatically, or you can right-click the tray icon and manually snip a chosen monitor.

![QR-deFuzzer Icon](assets/icon.png)

## Features

- System tray app with single-instance protection.
- Automatic QR scan across connected monitors.
- Multi-monitor handling for different monitor positions and resolutions.
- Manual snip mode with a monitor selector.
- QR decoding through ZXing.Net.
- `otpauth://` parsing for TOTP enrolment QR codes.
- Secret, issuer, account, and raw text copy actions.
- 2FA details tab for algorithm, digit count, period/counter, label, and any additional QR parameters.
- URL detection with an open-in-browser action.
- Optional auto-copy to clipboard.
- Optional run-on-startup support.
- Tray settings window with update checks, daily auto-check toggle, help/about, and screenshot cache controls.
- Debug screenshot saving is disabled by default and can be enabled only when needed.
- Machine-wide MSI installer and portable EXE release.

## Download

Download the latest release from the [Releases](../../releases) page.

Release assets:

- `QR-deFuzzer-Portable.exe` - portable self-contained EXE.
- `QR-deFuzzer-Setup.msi` - machine-wide MSI installer.

Publisher metadata is set to `Ghost Kernel`, with product information pointing to `https://ghostkernel.cc`.

## Usage

1. Launch QR-deFuzzer.
2. Use the tray icon:
   - Left-click: scan screens automatically for QR codes.
   - Right-click: open the menu.
3. For manual selection, choose `Manual Snip`, then pick the monitor you want.
4. Drag around the QR code and release.
5. Copy the decoded value, TOTP secret, issuer, account, or open a decoded URL.
6. For 2FA QR codes, use the `Details` tab to inspect extra parameters included in the QR code.

Press `Esc` or right-click during manual snip mode to cancel.

## Tray Menu

- `Scan Screens for QR` scans connected monitors automatically.
- `Manual Snip` opens a monitor selector for manual capture.
- `Settings...` opens update, help/about, startup, auto-copy, and cache controls.
- `Exit` closes the tray app.

## MSI Deployment

The MSI is a machine-wide install. It installs to:

```text
%ProgramFiles%\QR-deFuzzer
```

Silent install:

```powershell
msiexec /i QR-deFuzzer-Setup.msi /qn /norestart
```

Silent install with Windows startup enabled:

```powershell
msiexec /i QR-deFuzzer-Setup.msi /qn /norestart STARTUP=1
```

Silent uninstall:

```powershell
msiexec /x QR-deFuzzer-Setup.msi /qn /norestart
```

For Action1 or another RMM, deploy the MSI in the default managed/SYSTEM context. `STARTUP=1` writes a machine-wide startup entry to `HKLM\Software\Microsoft\Windows\CurrentVersion\Run`, so QR-deFuzzer starts for users when they sign in.

## Action1 / RMM Deployment

QR-deFuzzer is packaged as a machine-wide MSI. For tools such as Action1, the default managed/SYSTEM context is the right choice.

- Use the MSI, not the portable EXE, for managed deployment.
- Use `STARTUP=1` if you want QR-deFuzzer to start when users sign in.
- The app still writes logs and optional debug screenshot cache under each user's `%LocalAppData%` when it runs.

### Action1 Install Script

Upload `QR-deFuzzer-Setup.msi` to Action1 and deploy it as a Windows Installer package.

Additional MSI properties:

```text
STARTUP=1
```

Action1 command preview should look similar to:

```powershell
msiexec.exe /i "QR-deFuzzer-Setup.msi" /quiet /qn /norestart STARTUP=1
```

This installs QR-deFuzzer to:

```text
%ProgramFiles%\QR-deFuzzer
```

and registers startup here:

```text
HKLM\Software\Microsoft\Windows\CurrentVersion\Run\QR-deFuzzer
```

### Install Without Startup

```powershell
msiexec /i "QR-deFuzzer-Setup.msi" /qn /norestart
```

Users can later enable per-user startup from `Settings > General`, but managed deployments should prefer `STARTUP=1`.

### Update Existing Installs

Deploy the newer MSI with the same command. The installer uses a major upgrade, so it replaces the older install:

```powershell
msiexec /i "QR-deFuzzer-Setup.msi" /qn /norestart STARTUP=1
```

### Uninstall

```powershell
msiexec /x "QR-deFuzzer-Setup.msi" /qn /norestart
```

### Detection Hints

For Action1 detection rules, check one of these machine-wide locations:

```text
%ProgramFiles%\QR-deFuzzer\QR-deFuzzer.exe
HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall\QR-deFuzzer
```

The installed app registration includes `DisplayVersion`, so the uninstall registry key can be used for version checks.

## Logs

Logs are written to:

```text
%LocalAppData%\QR-deFuzzer\QR-deFuzzer.log
```

This is the first place to check if the tray icon does not appear, a monitor is not captured correctly, or QR decoding fails.

## Screenshot Cache

QR-deFuzzer does not save screenshots by default. In `Settings`, enable `Save screenshots for debugging` if you need cached captures while troubleshooting.

When enabled, screenshots are written to:

```text
%LocalAppData%\QR-deFuzzer\Cache
```

The Settings window can clear this folder manually, or auto-delete cached screenshots after a configured number of minutes.

## Updates

Use `Settings...` from the tray menu to check for updates. If a newer GitHub release is available, QR-deFuzzer can download the latest MSI and launch the installer.

QR-deFuzzer can also check for updates automatically once per day on startup. This is enabled by default and can be toggled in the `Updates` tab.

## Building

Prerequisites:

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [WiX Toolset](https://wixtoolset.org/) for MSI builds

Install WiX as a global .NET tool if needed:

```powershell
dotnet tool install --global wix
```

Build everything:

```powershell
.\build.ps1
```

Build portable only:

```powershell
.\build.ps1 -SkipInstaller
```

Build MSI only:

```powershell
.\build.ps1 -SkipPortable
```

Debug build:

```powershell
dotnet build
```

## Tech Stack

- C# / .NET 9
- WPF for result UI
- WinForms `NotifyIcon` for tray integration
- ZXing.Net for QR decoding
- WiX Toolset for MSI packaging

## License

MIT
