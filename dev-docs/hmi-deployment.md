# Deploying the operator HMI

The HMI runs on the CX51x0 itself and reaches the PLC locally over ADS. A Release build connects to
the local PLC; a Debug build connects to the controller at `5.86.238.52.1.1` so it can be run from a
development PC.

## Before the first deployment

Confirm the controller runs Windows, not TwinCAT/BSD: the HMI is a WPF application and needs
Windows.

## Publish

From the repo root:

```bash
dotnet publish HB1971A_HMI/HB1971A_HMI/HMI/HB1971A_HMI.csproj -c Release -r win-x64 --self-contained true
```

The output is in `HB1971A_HMI/HB1971A_HMI/HMI/bin/Release/net10.0-windows/win-x64/publish/`. It is
self-contained, so the controller needs no .NET runtime installed.

## Install on the controller

1. Close the HMI if it is running (About → Exit Application).
2. Copy the contents of the publish folder to `C:\HMI\` on the controller, replacing what is
   there.
3. Put a shortcut to `C:\HMI\HB1971A_HMI.exe` in
   `C:\Program Files (x86)\Beckhoff\TwinCAT\3.1\Target\StartUp` (TwinCAT 3.1 build 4026), so it
   starts with TwinCAT. It opens full screen on the Operator tab.

## Checks on the panel

- Tapping a setting opens the numeric keypad (`usePopupDialog: true` in `MainWindow`'s
  `UpdateControlSetup` call).
- A held touch on Forward starts the index at once and keeps it running for a full index, with no
  right-click circle first (press-and-hold is off for the whole window).
- After a power cycle the HMI comes up on its own and the top bar shows the machine status once
  the PLC runs.
- The PLC task shows no overruns with the HMI running.

## Leaving the HMI

- **Exit Application** (About page) closes the HMI; the machine keeps running and the pushbuttons
  still work. Restart it from `C:\HMI\HB1971A_HMI.exe`.
- **Shut Down Controller** (About page) sets `MAIN.ShutdownRequested`: the PLC saves its persistent
  settings and shuts the controller down. Stop the table first.
