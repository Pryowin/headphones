# HeadphoneStatus

A small Windows system tray app that shows whether your Logitech G535 LIGHTSPEED
headset is powered on or off — a **green** headphone icon means it's on, a
**greyed-out** icon means it's off (or unreachable). When it's on, hovering
over the icon also shows its battery percentage.

It works whether or not Logitech G HUB is installed or running: it talks
directly to the wireless receiver over raw HID, the same way G HUB itself would,
instead of depending on G HUB's own status reporting.

## How it works

The G535's USB receiver speaks Logitech's HID++ 2.0 protocol over a vendor HID
interface. The app periodically sends a small status request to that
interface and checks the receiver's reply:

- If the headset is on, the receiver answers with a normal feature response.
- If the headset is off (or out of range), the receiver answers with an
  HID++ error frame instead, because it can't reach the headset over its
  2.4GHz link.

This byte-level behavior was verified directly against the hardware (by
toggling the headset's power switch and observing the responses) rather than
assumed from documentation, so it should be reliable across G HUB being open,
closed, or not installed at all.

When the headset is on, the same reply also carries its battery voltage. The
app converts that voltage to a percentage using Logitech's standard Li-Poly
voltage curve (the same one used by the open-source Solaar project) and shows
it in the tray icon's tooltip — this was cross-checked against G HUB's own
reported battery level on real hardware.

## Requirements

- Windows 10 or 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (to build)
- A Logitech G535 LIGHTSPEED headset and its USB receiver plugged in

## Building

```powershell
cd HeadphoneStatus
dotnet build -c Release
```

The executable is produced at:

```
HeadphoneStatus\bin\Release\net8.0-windows\HeadphoneStatus.exe
```

## Running

Just launch `HeadphoneStatus.exe`. It has no window — it adds a headphone
icon to your system tray and updates it every few seconds. Hover over the
icon to see its status (and battery percentage, if it's on) in the tooltip.
Right-click the icon for a status line, a "Check now" option, and "Exit".

### Making the tray icon always visible

Windows may initially hide the icon in the tray overflow (the `^` arrow).
To keep it always visible next to the clock:

- **Windows 11**: Settings → Personalization → Taskbar → "Other system tray
  icons" → turn **HeadphoneStatus** on.
- **Windows 10**: Settings → Personalization → Taskbar → "Select which icons
  appear on the taskbar" → turn **HeadphoneStatus** on.
- Or just drag the icon out of the `^` overflow flyout onto the main tray row.

Windows remembers this choice for future launches as long as you keep running
the same `HeadphoneStatus.exe` path.

### Running automatically at login

Create a shortcut to the built exe in your Startup folder:

```powershell
$exe = "$PWD\HeadphoneStatus\bin\Release\net8.0-windows\HeadphoneStatus.exe"
$shortcutPath = Join-Path ([Environment]::GetFolderPath('Startup')) "HeadphoneStatus.lnk"
$ws = New-Object -ComObject WScript.Shell
$shortcut = $ws.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = Split-Path $exe
$shortcut.Save()
```

To stop it from launching at login, press `Win+R`, type `shell:startup`, and
delete `HeadphoneStatus.lnk`.

## Troubleshooting

- **Icon stays grey / says "receiver not found"**: make sure the USB receiver
  is plugged in. `Get-PnpDevice -PresentOnly | Where-Object FriendlyName -match
  'G535'` in PowerShell should list it.
- **Icon says "status unknown"**: the receiver responded, but not in either of
  the two shapes the app recognizes. This can happen briefly while the headset
  is reconnecting; it should resolve within a few seconds.
