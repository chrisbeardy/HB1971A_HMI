---
prd: 63b93a62
date: 2026-10-20
---

# Implementation Plan: Quick Operator HMI

> Plan for [PRD: Quick Operator HMI](63b93a62-quick-operator-hmi.md)

## Overview

Two repos change, PLC first because the HMI binds to the new symbols.

- **PLC** (`1971A_PLC`): `HMI_Forward` and `HMI_Reverse` in `FB_RotaryTable` beside its
  pushbuttons, `HMI_Reset` in `FB_Machine` beside its Reset button, each ORed with the pushbutton
  so they act as one set. The table's `Reset()` clears a stuck HMI press. `FB_Machine` gains
  `StatusText`, the machine status the HMI shows, as 1693A's `Machine.Text`.
- **HMI** (`1971A_HMI`, the HB WPF template already in the repo): a new **Operator** tab, left-most
  and the start page, built from bound HB controls only — machine status text, at station,
  momentary Reverse / Reset / Forward, and the speed and ramp settings with input limits. The
  **About** page gains the HB logo, **Exit Application** and **Shut Down Controller**
  (`MAIN.ShutdownRequested`). Every PLC variable used is added to the remote server. The whole HMI
  is laid out for the 1024×768 panel.
- **`twincat` skill**: the `HMI_` prefix convention.

## Architecture Decisions

No ADRs exist yet in either repo. Decisions taken with the user on 2026-10-20, worth recording as
ADRs (offered after acceptance):

- **Machine status is a PLC text, like 1693A.** `MAIN.Machine.StatusText` is the one status the
  HMI shows. Today it is the table's text, which already names the state and what to press; the
  machine FB is where machine-level conditions go as the rig grows. No status word, colour or
  HMI-side mapping: FR-01 is narrowed.
- **HMI buttons are momentary HB buttons acting as the pushbuttons.** No heartbeat, watchdog or
  source tracking; the HMI runs locally and the user accepts the risk of a stuck press. FR-08 and
  NFR-02 are out of scope; FR-06 is narrowed to "one set" (either source's button keeps a move
  going). The table's `Reset()` clears a stuck Forward/Reverse while not indexing.
- **`HMI_` prefix** for variables only the HMI reads or writes, declared locally in the FB that uses
  them. Added to the `twincat` skill.
- **Settings are written directly and limited at the HMI.** The HB number boxes refuse
  out-of-range entry, with limits read from the PLC (`MaxVelocity`, `MIN_RAMP_TIME`,
  `MAX_RAMP_TIME`). The PLC's existing ramp
  checks stay as a backstop; no refusal reasons on screen until the log is shown on the HMI
  (FR-10 narrowed). The log cannot tell an HMI change from an XAE one (NFR-07 partly met).
- **The HMI holds no logic.** Bound HB controls; the only code-behind is the Exit click. The header
  is bound straight to `MAIN.Machine.StatusText`, with no connection status of the HMI's own.

## Technical Approach — PLC

Repo `1971A_PLC`, project `HB1971_PLC/HB1971_PLC/_PLC/`. Follows the `twincat` skill: PascalCase,
`THIS^.` for own members, TcLog; edits checked with `tc_edit.py check` and `check_plcproj.py`;
files stay UTF-8 with BOM and CRLF. No new POUs and no I/O links, so no `.plcproj` or `.xti`
changes. Index velocity gets no PLC change: the HMI limits it, and the drive already refuses an
index above `MaxVelocity`.

### `FB_RotaryTable` — HMI Forward and Reverse as the same buttons

```iecst
VAR
    …
    ForwardButton : FB_DigitalInput;
    ReverseButton : FB_DigitalInput;
    HMI_Forward : BOOL; // the HMI's Forward: held as the Forward pushbutton is
    HMI_Reverse : BOOL;
    ForwardOn : BOOL; // Forward pushbutton or HMI Forward held
    ReverseOn : BOOL;
    ForwardPress : R_TRIG;
    ReversePress : R_TRIG;
    HmiForwardTrig : TcLogTrig; // logs HMI Forward presses and releases
    HmiReverseTrig : TcLogTrig;
    …
END_VAR
```

Body — every `ForwardButton.RE/.ON` and `ReverseButton.RE/.ON` moves to the combined signals
(edges at lines 89–94, hold checks at 142 and 145):

```iecst
ForwardButton();
ReverseButton();
ForwardOn := ForwardButton.ON OR HMI_Forward;
ReverseOn := ReverseButton.ON OR HMI_Reverse;
ForwardPress(CLK := ForwardOn);
ReversePress(CLK := ReverseOn);
HmiForwardTrig.OnRisingEdge(HMI_Forward).Information('HMI Forward pressed');
HmiForwardTrig.OnFallingEdge(HMI_Forward).Information('HMI Forward released');
// … same for Reverse
…
IF ForwardPress.Q THEN
    THIS^.RequestIndex(1, TRUE);
END_IF
IF ReversePress.Q THEN
    THIS^.RequestIndex(-1, TRUE);
END_IF
…
HoldReleased := HeldIndex AND NOT ForwardOn;   // Indexing, forward
HoldReleased := HeldIndex AND NOT ReverseOn;   // Indexing, reverse
```

`Reset()` clears a stuck HMI press — the table already knows when Reset is pressed:

```iecst
ResetRequested := TRUE;
// Clears an HMI press left held by a closed HMI. During an index Reset is ignored, like any other press.
IF NOT Busy THEN
    HMI_Forward := FALSE;
    HMI_Reverse := FALSE;
END_IF
Reset := TRUE;
```

Header comment gains one sentence: the HMI's Forward and Reverse act as the same buttons. A press
during a move is already unconsumed and logged as refused, and Reset during a move logged as
ignored, so FR-06 holds for the combined set.

### `FB_Machine` — HMI Reset and machine status

```iecst
VAR_OUTPUT
    StatusText : T_MAXSTRING; // what the machine is doing, or what the operator needs to do
END_VAR
VAR
    …
    ResetButton : FB_DigitalInput;
    HMI_Reset : BOOL; // the HMI's Reset: held as the Reset pushbutton is
    ResetPress : R_TRIG; // Reset pushbutton or HMI Reset pressed
    HmiResetTrig : TcLogTrig;
    …
END_VAR
```

```iecst
ResetButton();
ResetPress(CLK := ResetButton.ON OR HMI_Reset);
HmiResetTrig.OnRisingEdge(HMI_Reset).Information('HMI Reset pressed');
HmiResetTrig.OnFallingEdge(HMI_Reset).Information('HMI Reset released');

IF ResetPress.Q THEN
    Table.Reset();
END_IF

Table(Drive := TableDrive, DriveRamps := TableDrive, SafetyOK := iSafetyOK);
StatusText := Table.StatusText;

// very crude safety reset logic {warning 'TODO safety logic in standrd FB'}
qSafetyReset := ResetButton.ON OR HMI_Reset;
```

`StatusText` is the machine's own output, like the table's, so it takes no `HMI_` prefix.

### Symbols the HMI uses

| Symbol | Access | HMI use |
|---|---|---|
| `MAIN.Machine.StatusText` (**new**) | read | Machine status (FR-01, FR-02) |
| `MAIN.Machine.Table.OnDwell` | read | At station (FR-03) |
| `MAIN.Machine.Table.HMI_Forward`, `HMI_Reverse` (**new**) | write | Momentary Forward / Reverse |
| `MAIN.Machine.HMI_Reset` (**new**) | write | Momentary Reset |
| `MAIN.Machine.Table.IndexVelocity` (persistent) | read/write | Index speed, Hz |
| `MAIN.Machine.TableDrive.MaxVelocity` (persistent) | read | Index speed limit |
| `MAIN.Machine.TableDrive.MIN_RAMP_TIME`, `MAX_RAMP_TIME` (`VAR CONSTANT`) | read | Ramp time limits |
| `MAIN.Machine.Table.AccelerationTimeToIndexVelocity`, `DecelerationTimeFromIndexVelocity` | read/write | Ramps as s to/from index speed |
| `MAIN.Machine.TableDrive.AccelerationTimeToMaxFrequency`, `DecelerationTimeFromMaxFrequency` (persistent) | read/write | Ramps as the drive has them, F002/F003 |
| `MAIN.ShutdownRequested` | write TRUE | Saves persistents, then `NT_Shutdown`. Never cleared by the PLC |

## Technical Approach — HMI

Repo `1971A_HMI`, project `HB1971A_HMI/HB1971A_HMI/HMI/` (`HB1971A_HMI.csproj`, net10.0-windows,
HBControls 1.0.10). HB controls need no XAML prefix; `PlcReference` defaults to `PLC1`.

### 1024×768 throughout

The window is already 1024×768 maximised and borderless. Header 80 + status bar 40 + nav 72 leaves
576 × 1024 px for each screen. Design-time sizes (`d:DesignWidth/Height`) on the window and
every screen are set to the panel's, so the designer shows what the operator sees. Every screen
fits without scrolling; the About page keeps its `ScrollViewer` with `PanningMode="VerticalOnly"`
so a finger can scroll it if a theme or density makes it taller. Three nav buttons: Operator,
PlcTreeWalker and About; the template's empty Diagnostics screen and its unused model are deleted.

At the Touch density every HB labelled control has a fixed width of 500 and draws its borders by
`Position`: `Single` is boxed on its own; `Top`, `Middle` (the default) and `Bottom` stack into one
table with shared borders, a `Middle` drawing no bottom edge, and the style sets their margins. So
a lone labelled control takes `Position="Single"`, a stack takes Top/Middle…/Bottom with no
margins of its own, and a control in a narrower space takes `Width="Auto"`.

### Template clean-up

- Delete `HMI/HMI.csproj`: the wizard's throw-away project left beside the real one (breaks
  `dotnet build` in that folder and shares its `obj/`).
- Copy `C:\Users\chrisb\Huxley Bertram\WP (K) Sharepoint - Documents\HBEL-Logo\Huxley Bertram Logo\HB Logo No Background.png`
  to `HMI/Resources/`. The header already references this name, so this also fixes the header logo.
- Replace the template's two `AddRemoteServerVariable` lines (`MAIN.currentMainIndex`,
  `MAIN.testEnum` do not exist) with the variables below.

### `MainWindow`

- New first tab and nav button; PlcTreeWalker and About move to Tags 1–2. The `TabControl` starts
  on index 0, so Operator is the start page.
- `Stylus.IsPressAndHoldEnabled="False"` on the window. `FrameworkElement` makes it inherited, so
  it reaches every control's template; without it a held touch waits to become a right-click
  before the hold-to-run buttons see the press (seen on the panel).

```xml
xmlns:operator="clr-namespace:HB1971A_HMI.Screens.Operator"
…
<TabItem>
    <operator:OperatorControl />
</TabItem>
…
<HBStaticButton Content="Operator" Glyph="&#xE80F;" Tag="0" Height="72" Click="TouchNav_Click" />
```

- Remote server variables — every PLC variable the HMI uses:

```csharp
SystemHandler.AddRemoteServerVariable(PLCID, "Status",                      "MAIN.Machine.StatusText");
SystemHandler.AddRemoteServerVariable(PLCID, "At Station",                  "MAIN.Machine.Table.OnDwell");
SystemHandler.AddRemoteServerVariable(PLCID, "HMI Forward",                 "MAIN.Machine.Table.HMI_Forward");
SystemHandler.AddRemoteServerVariable(PLCID, "HMI Reverse",                 "MAIN.Machine.Table.HMI_Reverse");
SystemHandler.AddRemoteServerVariable(PLCID, "HMI Reset",                   "MAIN.Machine.HMI_Reset");
SystemHandler.AddRemoteServerVariable(PLCID, "Index Velocity",              "MAIN.Machine.Table.IndexVelocity");
SystemHandler.AddRemoteServerVariable(PLCID, "Max Velocity",                "MAIN.Machine.TableDrive.MaxVelocity");
SystemHandler.AddRemoteServerVariable(PLCID, "Ramp Time Min",               "MAIN.Machine.TableDrive.MIN_RAMP_TIME");
SystemHandler.AddRemoteServerVariable(PLCID, "Ramp Time Max",               "MAIN.Machine.TableDrive.MAX_RAMP_TIME");
SystemHandler.AddRemoteServerVariable(PLCID, "Acceleration To Index Speed", "MAIN.Machine.Table.AccelerationTimeToIndexVelocity");
SystemHandler.AddRemoteServerVariable(PLCID, "Deceleration From Index Speed","MAIN.Machine.Table.DecelerationTimeFromIndexVelocity");
SystemHandler.AddRemoteServerVariable(PLCID, "Acceleration F002",           "MAIN.Machine.TableDrive.AccelerationTimeToMaxFrequency");
SystemHandler.AddRemoteServerVariable(PLCID, "Deceleration F003",           "MAIN.Machine.TableDrive.DecelerationTimeFromMaxFrequency");
SystemHandler.AddRemoteServerVariable(PLCID, "Shutdown Requested",          "MAIN.ShutdownRequested");
```

- `PLC1_ADDR`: the controller when debugging from a development PC, local when published:

```csharp
#if DEBUG
        private const string PLC1_ADDR = "5.86.238.52.1.1";  // the 1971A controller, for running from a development PC
#else
        private const string PLC1_ADDR = "";  // empty = local PLC
#endif
```

- **Header: the machine status.** The template's header label, bound straight to the PLC:
  `HBStandardLabel GetVariable="MAIN.Machine.StatusText" PreventTranslation="True"`, FontSize 22
  SemiBold, with a wrapping, centred, ellipsis-trimmed `TextBlock` `ContentTemplate`. No code: the
  HMI does not report its own connection (the user's call; FR-01 and FR-13 narrowed). Logos at
  Height 56 leave about 600 px, so the longest message (`Indexing forward stopped: button released
  before dwell - hold Forward or Reverse to continue`) fits on two lines in the 80 px header
  (checked by an off-screen render).

- `usePopupDialog: true`: the HB number and text boxes open their entry popup (the on-screen
  keypad) instead of taking typed entry in place. The panel has no keyboard.

### `Screens/Operator/OperatorControl.xaml` (new)

Code-behind is the template's `InitializeComponent()` only. The machine status is in the header,
on every page. Grid: at station and the buttons on the left (`*`), the settings card on the right
(`Auto`).

1. **At station** — `HBLabelledStatusIndicator Label="At station" GetVariable="MAIN.Machine.Table.OnDwell"
   Position="Single" Width="Auto" HorizontalAlignment="Stretch" ShowStatusText="False" IndicatorSize="32"`.
2. **Buttons** — a one-row `UniformGrid`, Reverse | Reset | Forward, each ~140 px high:

```xml
<HBStandardButtonToggle Content="Forward" Glyph="&#xE72A;" UserAction="Momentary"
    SetVariable="MAIN.Machine.Table.HMI_Forward" SetOnContent="True" SetOffContent="False"
    Height="140" Margin="8" />
```

   Reverse: `&#xE72B;`, `MAIN.Machine.Table.HMI_Reverse`. Reset: `&#xE72C;`, `MAIN.Machine.HMI_Reset`.
3. **Settings** — a bordered card in the About page's style, one `HBLabelledNumberBox` per setting
   stacked as one table (`Position` Top, Middle ×3, Bottom; `LabelWidth="270"` so the longest label
   fits), limited so a value outside the range cannot be entered:

| Label | `SetVariable` | Unit | DP | Limits |
|---|---|---|---|---|
| Index speed | `MAIN.Machine.Table.IndexVelocity` | Hz | 1 | `Minimum="0.1"`, `MaximumValue="MAIN.Machine.TableDrive.MaxVelocity"` |
| Acceleration to index speed | `MAIN.Machine.Table.AccelerationTimeToIndexVelocity` | s | 2 | `MinimumValue="MAIN.Machine.TableDrive.MIN_RAMP_TIME"`, `MaximumValue="MAIN.Machine.TableDrive.MAX_RAMP_TIME"` |
| Deceleration from index speed | `MAIN.Machine.Table.DecelerationTimeFromIndexVelocity` | s | 2 | as above |
| Drive acceleration, F002 | `MAIN.Machine.TableDrive.AccelerationTimeToMaxFrequency` | s | 2 | as above |
| Drive deceleration, F003 | `MAIN.Machine.TableDrive.DecelerationTimeFromMaxFrequency` | s | 2 | as above |

   The limits come from the PLC. `MIN_RAMP_TIME` / `MAX_RAMP_TIME` are `FB_OmronMX2`'s
   `VAR CONSTANT`s; checked with pyads on 2026-10-20 they read by name over ADS (0.01, 3600.0), with
   symbol info, as does `MaxVelocity` (75.0). The to-index-speed forms scale by
   `IndexVelocity / RampVelocity` before the drive checks them, so a value inside these limits can
   still be refused; it then snaps back to the value in force on the next poll.

### `Screens/About/AboutControl`

Two columns so the page fits 1024×576: **left** — HB logo, Software Information, the Huxley
Bertram footer; **right** — Theme, Language, Persistent Data and a new **System** card.

- **HB logo**: `<Image Source="/Resources/HB Logo No Background.png" Height="80" HorizontalAlignment="Left" Margin="0,0,0,16" RenderOptions.BitmapScalingMode="HighQuality" />`.
- **System** card:

```xml
<HBStandardButton Content="Exit Application" Glyph="&#xE8BB;" MinWidth="180"
    ConfirmMode="Dialog" ConfirmationTitle="Exit application"
    ConfirmationMessage="Close the operator screen? The machine keeps running."
    Click="ExitButton_Click" />
<HBStandardButton Content="Shut Down Controller" Glyph="&#xE7E8;" MinWidth="180"
    SetVariable="MAIN.ShutdownRequested" SetContent="True" ConfirmMode="Dialog"
    ConfirmationTitle="Shut down"
    ConfirmationMessage="Stop the table first. The PLC saves its settings, then the controller shuts down." />
```

```csharp
private void ExitButton_Click(object sender, RoutedEventArgs e)
{
    Application.Current.Shutdown();
}
```

An `HBStandardButton` with `ConfirmMode="Dialog"` raises `Click` only once the operator confirms,
so Exit uses the same themed dialog as Shutdown with no dialog code of its own.
`Application.Current.Shutdown()` runs the existing `Window_Closing` clean-up.

### Touch press-and-hold

Neither the HB control reference (1906 SharePoint) nor the library code handles touch: Momentary
writes from `OnMouseDown`/`OnMouseUp`, relying on WPF promoting a touch to a mouse press. WPF's
press-and-hold (right-click emulation) holds that back while a finger stays down, so it is off for
the whole window (see `MainWindow`). Worth raising for HBControls, where the Momentary button could
set it itself.

### Deployment (FR-13)

Recorded in `dev-docs/hmi-deployment.md`:

1. Confirm the CX51x0 runs Windows (not TwinCAT/BSD): WPF needs Windows.
2. `dotnet publish HB1971A_HMI/HB1971A_HMI/HMI/HB1971A_HMI.csproj -c Release -r win-x64 --self-contained true -o publish`
   (no .NET 10 runtime install needed on the CX); copy to `C:\HMI\` on the controller.
3. Shortcut to `C:\HMI\HB1971A_HMI.exe` in the TwinCAT `Target\StartUp` folder (the method used on
   1753A), so it starts with TwinCAT; it is already full screen. On build 4026 that folder is
   `C:\Program Files (x86)\Beckhoff\TwinCAT\3.1\Target\StartUp`; on 4024 (as on 1753A) it is
   `C:\TwinCAT\3.1\Target\StartUp`.

## Technical Approach — `twincat` skill

`C:\Users\chrisb\.claude\skills\twincat\`:

- `SKILL.md`: a row in "The short list" and a bullet under **Naming**, next to the `i`/`q` rule.
- `references/naming-and-style.md`: a section "The `HMI_` prefix" after "The `i` / `q` I/O prefix":
  - `HMI_` marks a variable that only the HMI reads or writes — `HMI_Forward`, `HMI_Reset`. Like
    `i`/`q` it is a deliberate exception to no-Hungarian: it tells the reader the value comes from,
    or exists for, something outside the PLC program.
  - Declare it in the FB that uses it, as a plain `VAR` beside the input it mirrors — not in a GVL.
  - The PLC can still validate, limit or clear it (e.g. a stuck momentary press cleared on Reset);
    anything may be written into it at any moment.
  - Not for the FB's own outputs that the HMI happens to display (`StatusText`).

## Files to Create

| File | Purpose |
|---|---|
| `1971A_HMI: HB1971A_HMI/HB1971A_HMI/HMI/Screens/Operator/OperatorControl.xaml` (+ `.xaml.cs`) | The Operator tab |
| `1971A_HMI: HB1971A_HMI/HB1971A_HMI/HMI/Resources/HB Logo No Background.png` | HB logo for the header and About page |
| `1971A_HMI: dev-docs/hmi-deployment.md` | Publish and auto-start steps for the controller |

## Files to Modify

| File | Change |
|---|---|
| `1971A_PLC: HB1971_PLC/HB1971_PLC/_PLC/Machine/RotaryTable/FB_RotaryTable.TcPOU` | `HMI_Forward`/`HMI_Reverse` combined with the pushbuttons; `Reset()` clears them; HMI press logging |
| `1971A_PLC: HB1971_PLC/HB1971_PLC/_PLC/Machine/FB_Machine.TcPOU` | `HMI_Reset`, combined Reset, `StatusText`, `qSafetyReset`, logging |
| `1971A_PLC: CLAUDE.md` | "HMI" section: the `HMI_` variables, `StatusText`, Reset clearing a stuck HMI press |
| `1971A_HMI: HB1971A_HMI/HB1971A_HMI/HMI/MainWindow.xaml` (+ `.cs`) | Operator tab and nav; machine status in the header; press-and-hold off; `usePopupDialog`; remote server variables; `PLC1_ADDR`; design sizes |
| `1971A_HMI: HB1971A_HMI/HB1971A_HMI/HMI/Screens/About/AboutControl.xaml` (+ `.cs`) | Two columns; HB logo; System card with Exit and Shutdown; `PanningMode` |
| `1971A_HMI: HB1971A_HMI/HB1971A_HMI/HMI/Screens/PlcTreeWalker/*.xaml` | Design-time sizes only |
| `1971A_HMI: HB1971A_HMI/HB1971A_HMI/HMI/Screens/Diagnostics/*`, `Models/DiagnosticsModel.cs` | Delete (empty template stub, unused model) |
| `1971A_HMI: HB1971A_HMI/HB1971A_HMI/HMI/HMI.csproj` | Delete |
| `1971A_HMI: dev-docs/PRDs/63b93a62-quick-operator-hmi.md` | Bring FR-01, FR-06, FR-08, FR-10, NFR-02, NFR-07 and their success criteria in line with the decisions above; add the About page items |
| `~/.claude/skills/twincat/SKILL.md`, `references/naming-and-style.md` | `HMI_` prefix |

## Implementation Order

Branch `feature/quick-operator-hmi` from `dev` in **both** repos. The HMI repo's working tree
already holds the untracked template and the PRD status change; they carry onto the branch as part
of this work. No commits until `/prd done`.

1. **`twincat` skill: `HMI_` prefix** — first, so the PLC edits follow it.
2. **PLC: `FB_RotaryTable`**, then **`FB_Machine`**; `tc_edit.py check` and `check_plcproj.py`.
3. **PLC: `CLAUDE.md`** — HMI section.
4. **User: reload in XAE (without saving first), build, activate or online change.** Then bench
   check over ADS with pyads (asking before each write to the running machine): `HMI_Forward`
   TRUE then FALSE behaves as the pushbutton; Reset clears a left-over `HMI_Forward`;
   `MAIN.Machine.StatusText` matches the table's.
5. **HMI: template clean-up** — delete `HMI.csproj`, copy the logo.
6. **HMI: `MainWindow`** — Operator tab, machine status in the header, remote server variables, `PLC1_ADDR`, design sizes.
7. **HMI: `OperatorControl`**.
8. **HMI: About page**.
9. **Build** `dotnet build HB1971A_HMI/HB1971A_HMI/HB1971A_HMI.slnx` with no new warnings; run the
   Debug build from the dev PC against the controller for the bench checks below.
10. **Deployment note**, then the PRD update.

Steps 2–3 and 5–8 are independent; step 9 needs step 4 live.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| HMI crashes or loses ADS with a button held | A move runs on to dwell; that button's pushbutton gives no fresh edge until cleared | Accepted by the user. Reset clears a stuck Forward/Reverse; the HB button also writes off on mouse-up, lost capture, unload and disable |
| `HMI_Reset` left held by a crash | Pushbutton Reset gives no edge and `qSafetyReset` stays on | Accepted with the above; pressing and releasing HMI Reset clears it |
| HB momentary button does not release when the finger slides off (it keeps mouse capture) | FR-07's "leaves it": the move runs until the finger lifts | Accepted for now; the fix belongs in HBControls |
| WPF touch press-and-hold holds back the press | A held touch does not press until the right-click circle finishes (seen on the panel) | `Stylus.IsPressAndHoldEnabled="False"` on the window |
| Number box does not open the keypad on touch | Settings need a keyboard (NFR-06) (seen on the panel) | `usePopupDialog: true` |
| HB library resolves `MinimumValue`/`MaximumValue` through its symbol loader, which may not list constants (they are absent from the flat symbol upload) | Ramp limits not applied | Step 9 check: 0.001 s refused at entry. Fallback: literals 0.01 and 3600 |
| To-index-speed ramp inside the HMI limits but outside the drive's after scaling | Value refused by the PLC with no reason shown; it snaps back | Accepted until the log is on the HMI |
| CX runs TwinCAT/BSD, or lacks resources | WPF cannot run | Confirm the OS before deploying; self-contained publish |
| Shutdown pressed mid-index | TwinCAT stops while the MX2's comms-error action is still unset (CLAUDE.md open item) | Confirmation text says stop the table first |
| Debug build talks to the real controller | Bench presses move the real table; Shutdown shuts it down | Bench checks with guards and E-stop in force; do not accept the Shutdown dialog unless testing it |
| Remote server exposes the HMI buttons and Shutdown | Anything connected to it can press them | As requested; review before the controller goes on a wider network |
| Long status text overflows | Message clipped | Wrapping `ContentTemplate`; smaller font if two lines are not enough |

## Requirement Traceability

| Requirement | Plan Coverage | Notes |
|---|---|---|
| FR-01 | `FB_Machine.StatusText` bound straight to the header label | Text only, by decision |
| FR-02 | Operator status label | |
| FR-03 | At-station indicator (`Table.OnDwell`) | |
| FR-04 | `FB_RotaryTable` combined signals; HB momentary buttons | |
| FR-05 | `FB_Machine` `ResetPress`, `qSafetyReset`; HB momentary Reset | |
| FR-06 | Combined signals; existing refusal of presses during a move | Narrowed by decision: one set |
| FR-07 | HB momentary button writes off on lift / lost capture | Slide-off not covered (risk table) |
| FR-08 | — | Out of scope by decision; Reset clears a stuck press |
| FR-09 | Operator settings card | Both forms of each ramp |
| FR-10 | HB number box limits read from the PLC (`MaxVelocity`, `MIN_RAMP_TIME`, `MAX_RAMP_TIME`); existing PLC ramp checks | Narrowed: no reason shown |
| FR-11 | Existing persistent variables and save at shutdown | |
| FR-12 | HB polling (`StartUpdate(200)`) of the bound settings | |
| FR-13 | Deployment note (StartUp shortcut) | No connection status, by decision |
| FR-14 | About page: HB logo; Exit Application (`HBStandardButton`, `ConfirmMode="Dialog"`, `Click` → `Application.Current.Shutdown()`) | |
| FR-15 | About page: Shut Down Controller (`MAIN.ShutdownRequested`) | |
| NFR-01 | ORed inputs are FALSE with no HMI press, so behaviour is unchanged | Verified with the app closed |
| NFR-02 | — | Out of scope by decision |
| NFR-03 | ~12 polled symbols at 200 ms | Verified on the controller |
| NFR-04 | 200 ms poll | |
| NFR-05 | Status in words, large font; at-station labelled | |
| NFR-06 | `usePopupDialog: true`; press-and-hold off | |
| NFR-07 | `TcLogTrig` on the `HMI_` buttons | Setting changes logged without source |
| NFR-08 | `twincat` skill, `tc_edit.py check`, `check_plcproj.py` | |
| NFR-09 | Every screen laid out for 1024×576 under the header, status bar and nav; About in two columns | |
| SC-01 Statuses | Step 9: E-stop, fault, early release, index, ready | Shown as text |
| SC-02 Message matches online view | Step 4, step 9 | |
| SC-03 At station matches dwell cam | Step 9 | |
| SC-04 Hold-to-run criteria with HMI buttons | Step 9, then site | |
| SC-05 HMI Reset clears fault and safety | Step 9 | |
| SC-06 Other buttons no effect during a move | Step 9 | Per narrowed FR-06 |
| SC-07 Kill / disconnect during a held move | — | Out of scope by decision |
| SC-08 Speed and ramps changed, next index uses them | Step 9 | |
| SC-09 Out-of-range value refused | Step 9 | Refused at entry by the HMI limits |
| SC-10 Survives power cycle | Site | |
| SC-11 XAE change appears on the HMI | Step 9 | |
| SC-12 Auto-start after power cycle | Site | |
| SC-13 Physical buttons with app closed | Step 9 | |
| SC-14 No PLC overruns | Site | |
| SC-15 Log shows HMI presses | Step 4, step 9 | Presses and releases only |

## Verification

1. **PLC, step 4** — after build and activate, with pyads (asking first): `HMI_Forward` TRUE starts
   an index, FALSE before dwell stops it early with the stopped-before-dwell text, TRUE again
   continues; leave `HMI_Forward` TRUE, press Reset with the table idle and see it cleared; check
   `D:\PLC_Logs\main_log.txt` for the HMI press and release lines.
2. **HMI, step 9, from the dev PC (guards and E-stop in force)** — opens on Operator at 1024×768;
   header shows "PLC running", then "PLC not running" when the PLC is stopped; hold Forward for a
   full index, release early, hold again to continue; HMI Reset after an E-stop resets safety;
   number boxes refuse 0 and values above `MaxVelocity`, refuse a ramp of 0.001 s (limits read from
   the PLC constants), and accept good values that the next
   index uses; a value changed in XAE appears; every tab fits without clipping; About shows the HB
   logo, Exit asks then closes, Shutdown asks (decline).
3. **Panel (site)** — keypad and press-and-hold checks; deploy, power-cycle for auto-start and
   persistence; watch the PLC task for overruns; hand-over to William.
