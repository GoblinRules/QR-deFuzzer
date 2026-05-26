# Changelog

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
