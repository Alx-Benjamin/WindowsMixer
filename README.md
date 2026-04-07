<div align="center">

# Windows Mixer

Per-app volume control for the Windows taskbar.<br>
Right-click any app or use your keyboard — without touching system volume.

[![Discord](https://img.shields.io/badge/Discord-Join-5865F2?logo=discord&logoColor=white)](https://discord.gg/MfW5Mt7KUe)
[![Website](https://img.shields.io/badge/Website-AlxBenjamin.com-0A7AFF?logo=safari&logoColor=white)](https://alxbenjamin.com)

[**Download**](https://github.com/Alx-Benjamin/WindowsMixer/releases) &nbsp;·&nbsp; [Source](#build-from-source) &nbsp;·&nbsp; [Discord](https://discord.gg/MfW5Mt7KUe)

<br>

</div>

---

## What It Does

Windows Mixer gives you per-application volume control directly from the Windows taskbar. Right-click any running app to get a slider for that app's audio — completely separate from system volume. Or hold **Ctrl** or **Shift** while pressing your hardware volume keys to control just the app you're focused on, without leaving your current window.

---

## Usage

### Taskbar Right-Click

Hover over any app button on the taskbar for a moment, then right-click it. A compact volume slider appears in the bottom-right corner of your screen.

- Drag the slider to set an exact level
- Scroll the mouse wheel to nudge volume up or down
- Click the speaker icon to mute or unmute

The popup follows the right-click menu and closes automatically a few seconds after you dismiss the menu.

### Keyboard Shortcut

Hold **Ctrl** or **Shift** while pressing your hardware volume up, down, or mute key. Windows Mixer intercepts the keypress before it reaches the OS — system volume stays unchanged — and applies the adjustment only to the app that currently has focus. The volume slider pops up so you can see the level as it changes.

---

## Features

- Per-app volume slider, independent of system volume
- Mute toggle with speaker glyph
- Mouse wheel support anywhere on the popup
- Ctrl/Shift + volume key shortcut — controls active app only
- Automatically matches Windows 11 light and dark themes
- Runs silently in the system tray with no taskbar entry
- Single portable EXE, no installation required

---

## Tech Stack

| | |
|---|---|
| Language | C# / .NET 6 |
| UI | Windows Forms |
| Audio | NAudio 2 (WASAPI) |
| Input hooks | `WH_MOUSE_LL` / `WH_KEYBOARD_LL` |
| Theme | DWM immersive dark mode + registry personalisation |
| Distribution | Single self-contained EXE via PublishSingleFile |

---

## How It Works

**App identification:** Every 150ms, Windows Mixer scans for a window titled "Jump List for [AppName]" — the tooltip shell Windows generates when hovering a taskbar button. This identifies which app the cursor is over and caches it with the cursor position.

**Right-click detection:** A `WH_MOUSE_LL` low-level mouse hook fires on every right-click. If the click lands within the taskbar rect, the app from the hover cache is matched (within ~150px of the hover point) and its WASAPI audio sessions are looked up.

**Keyboard interception:** A `WH_KEYBOARD_LL` hook watches every key event. Modifier state (Ctrl/Shift) is tracked in the hook itself for reliability. When a volume key fires with a modifier held, the keypress is consumed — returning `1` from the hook proc so the OS never sees it — and the foreground window's audio sessions are adjusted instead.

**Volume control:** All audio is routed through the Windows WASAPI session API via NAudio, the same layer the Windows Volume Mixer uses. Changes are per audio session, so no other app is affected.

---

## Getting Started

Download `WindowsMixer.exe` from the [Releases](https://github.com/Alx-Benjamin/WindowsMixer/releases) page and run it. No installer needed. Windows Mixer starts in the system tray — look for the speaker icon.

### Requirements

- Windows 10 or 11 (64-bit)
- No additional runtime required (self-contained)

---

## Build from Source

**Prerequisites:** .NET 6 SDK

```bash
git clone https://github.com/Alx-Benjamin/WindowsMixer.git
cd WindowsMixer
dotnet publish WindowsMixer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o bin/publish
```

The output EXE is at `bin/publish/WindowsMixer.exe`.

---

## Links

- [AlxBenjamin.com](https://alxbenjamin.com)
- [Discord](https://discord.gg/MfW5Mt7KUe)
- [GitHub](https://github.com/Alx-Benjamin/WindowsMixer)
