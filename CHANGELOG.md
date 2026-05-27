# Changelog

## v1.1.10 - 2026-05-27

- Fixed duplicate Programs & Features entries by removing the custom uninstall registry entry.
- The MSI now relies on Windows Installer's standard app registration.
- Added manual MSI install options for startup and default auto-copy.
- Added `AUTOCOPY=1` MSI property for Action1/RMM deployment.
- Startup detection now recognizes machine-wide Run entries even if the app was launched from another path.

## v1.1.9 - 2026-05-27

- Changed the MSI to a proper machine-wide installer for Action1/RMM deployment.
- MSI now installs to Program Files instead of the installing user's LocalAppData.
- `STARTUP=1` now writes a machine-wide HKLM Run entry so SYSTEM-context deployments start for users at sign-in.
- Updated Action1 deployment documentation.

## v1.1.8 - 2026-05-27

- Added automatic daily update checks on startup.
- Added a Settings toggle for daily update checks.
- Prompts to download and install the latest MSI when a newer release is available.

## v1.1.7 - 2026-05-26

- Restyled the tray right-click menu to match the QR-deFuzzer dark UI.
- Added dark menu backgrounds, borders, hover states, separators, and submenu arrows.

## v1.1.6 - 2026-05-26

- Moved startup, auto-copy, and about controls fully into Settings.
- Simplified the tray menu to scan, manual snip, settings, and exit actions.
- Restyled Settings tabs, buttons, fields, and window chrome to match the QR-deFuzzer dark UI.

## v1.1.5 - 2026-05-26

- Added a tray Settings window.
- Added update checking and MSI download/install from the latest release.
- Added help/about content inside Settings.
- Moved debug screenshots to a dedicated cache folder.
- Disabled screenshot saving by default; it can now be enabled only for debugging.
- Added cache clearing and optional screenshot auto-delete after a configured number of minutes.

## v1.1.4 - 2026-05-26

- Added a 2FA Details tab for OTP QR metadata and additional query parameters.

## v1.1.3 - 2026-05-26

- Reworked scanning around multi-monitor setups.
- Automatic scan now checks each distinct monitor capture and stops on the first QR result.
- Added manual snip as a right-click tray option with a monitor selector.
- Updated installer metadata to Ghost Kernel and `https://ghostkernel.cc`.
- Added MSI deployment property `STARTUP=1` for per-user startup registration.
- Release builds omit debug symbols and source paths.

## v1.1.2 and earlier

Earlier builds were superseded while stabilizing startup, installer registration, high-DPI handling, and multi-monitor capture behavior.
