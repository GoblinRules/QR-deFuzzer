# QR-deFuzzer

A lightweight Windows system tray application that lets you snip any area of your screen to decode QR codes instantly. Built for speed and security — features native 2FA `otpauth://` parsing with secret key extraction, perfect for adding accounts to Proton Pass, Google Authenticator, and other TOTP apps.

![QR-deFuzzer Icon](assets/icon.png)

## Features

- **System Tray App** — Runs quietly in the background. Left-click the tray icon or right-click for options.
- **Snipping Tool** — Drag to select any area of your screen containing a QR code.
- **Instant Decode** — Decodes QR codes in milliseconds using ZXing.Net.
- **2FA / OTPAuth Support** — Automatically parses `otpauth://totp/...` URIs and displays:
  - Issuer (e.g., Proton, GitHub, Google)
  - Account name
  - Secret key (masked by default, with reveal toggle)
  - Copy buttons for each field
- **URL Detection** — Detects web URLs and offers an "Open in Browser" button.
- **Auto-Copy to Clipboard** — Optionally auto-copies decoded text on capture.
- **Run on Startup** — Toggle automatic startup from the tray menu.
- **Single Instance** — Prevents duplicate instances via mutex.
- **Multi-Monitor Support** — Captures across all connected displays.

## Installation

### Portable (No Install Required)
Download `QR-deFuzzer-Portable.exe` from the [Releases](../../releases) page and run it. No dependencies needed — the .NET runtime is embedded.

### MSI Installer
Download `QR-deFuzzer-Setup.msi` from the [Releases](../../releases) page. This installs to `%LocalAppData%\QR-deFuzzer` and creates desktop + start menu shortcuts.

## Building from Source

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [WiX Toolset v7](https://wixtoolset.org/) (for MSI installer only)

### Quick Build
```powershell
# Build everything (portable + installer)
.\build.ps1

# Portable only
.\build.ps1 -SkipInstaller

# Installer only
.\build.ps1 -SkipPortable
```

### Manual Build
```powershell
# Debug build
dotnet build

# Portable single-file EXE
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o dist\portable

# MSI installer
wix build -src installer.wxs -d "PublishDir=dist\portable" -d "ProjectDir=." -o dist\QR-deFuzzer-Setup.msi
```

## Usage

1. Launch QR-deFuzzer — it appears as an icon in your system tray.
2. **Left-click** the tray icon (or select "Snip & Decode QR" from the right-click menu).
3. **Drag** to select the area containing a QR code.
4. The decoded result appears in a popup:
   - **Plain text/URL**: Copy or open in browser.
   - **2FA code**: View issuer, account, and secret key with individual copy buttons.
5. Press **Escape** at any time to cancel.

## Tech Stack

- **C# / .NET 9.0** — WPF for UI, WinForms for system tray (`NotifyIcon`)
- **ZXing.Net** — QR code decoding
- **WiX Toolset v7** — MSI installer packaging

## License

MIT
