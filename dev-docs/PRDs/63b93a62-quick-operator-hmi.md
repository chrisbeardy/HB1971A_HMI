---
name: Quick Operator HMI
status: done
date: 2026-10-21
tags: [hmi]
depends_on: []
---

# Quick Operator HMI

## Overview

A single screen at the machine tells the operator what the machine is doing and what, if
anything, they need to do. It carries on-screen Forward, Reverse and Reset buttons that work
exactly like the physical ones, and it lets the operator change the index speed and the
acceleration and deceleration. It is a quick, single-page Windows application running on the
controller, and it is deliberately kept small until a full HMI is needed.

This PRD depends on two PRDs in the PLC repo, both done: Resume After Early Release (cf7d6e53) for
the stopped-before-dwell status, and Adjustable Drive Ramps (30d5322a) for the ramp settings.

## Problem Statement

The machine has no screen. During his trial, William could not tell whether the machine was
waiting, stopped on safety or in error: "it doesn't tell you anything, you just have to wait
patiently". The status exists in the PLC, but only an engineer online in XAE can see it.

The settings William is likely to want to try during process development (index speed,
acceleration and deceleration) also need an engineer online in XAE. That slows every trial down
to the engineer's availability.

## Proposed Solution

**Status.** The top bar shows the machine status as the PLC's own plain-text message, in large
text so it can be read from the table: what the machine is doing, or what is wrong and what to
press. It is on every page. The machine status covers:

- Safety stop: E-stop pressed or a guard open, then reset the safety system
- Error: the reason, and press Reset
- Stopped before dwell: hold Forward or Reverse to continue
- Moving
- Ready

The top bar shows the PLC's text directly; the screen does not report its own connection to the
PLC. The operator page shows whether the table is at a station.

**Buttons.** On-screen Forward, Reverse and Reset do exactly what the physical buttons do.
Forward and Reverse are hold-to-run, one station per press, and continue after an early release.
Reset clears faults and resets the safety system. The screen and physical buttons act as one
set: a move continues while its button is held, on screen or physically, and every other press
is ignored until the move ends. The screen runs on the controller itself, so a button left held
by a screen that closes or crashes is an accepted risk: the move it holds runs on to the station,
and the next Reset with the table stopped clears it.

**Settings.** The operator can change the index speed and both ramps, each ramp in both of its
linked forms. The screen refuses entry outside the limits the PLC gives: the maximum index speed
and the shortest and longest ramp time. The PLC still checks every change, and a value it refuses
goes back to the value in force. Accepted changes are kept like the other table settings, saved
automatically when the controller shuts down.

**About page.** The About page shows the HB logo and two system buttons, each asking for
confirmation first: Exit Application closes the screen and leaves the machine running, and Shut
Down Controller saves the settings and shuts the controller down.

**Independence.** With no on-screen button held, the machine runs exactly the same whether the
screen is running, closed or crashed. The physical buttons always work.

## Requirements

### Functional Requirements

| ID | Requirement | Priority |
|----|-------------|----------|
| FR-01 | The top bar shows one overall machine status as the PLC's text: safety stop, error, stopped before dwell, moving or ready | Must |
| FR-02 | The screen shows the PLC's plain-text status or fault message, including what the operator needs to press | Must |
| FR-03 | The screen shows whether the table is at a station | Should |
| FR-04 | On-screen Forward and Reverse behave exactly like the physical buttons: hold-to-run, one station per press, a fresh press needed for each move, and continuing after an early release | Must |
| FR-05 | On-screen Reset behaves exactly like the physical Reset, including resetting the safety system | Must |
| FR-06 | On-screen and physical buttons act as one set. A move continues while its button is held, on screen or physically, and every other press is ignored until the move ends | Must |
| FR-07 | An on-screen button counts as released when the finger or pointer lifts off it | Must |
| FR-08 | An on-screen Forward or Reverse left held by an application that closed or crashed is cleared by the next Reset with the table stopped | Must |
| FR-09 | The operator can view and change the index speed, the acceleration and the deceleration, each ramp in both its drive-native and time-to-index-speed forms, linked as in Adjustable Drive Ramps | Must |
| FR-10 | The screen refuses entry outside the limits the PLC gives. The PLC still validates every change, and a value it refuses reverts on screen to the value in force | Must |
| FR-11 | A setting changed on the screen survives a power cycle, through the same automatic save at shutdown as the other persistent settings | Must |
| FR-12 | Values changed elsewhere, such as online in XAE, appear on the screen | Should |
| FR-13 | The application starts by itself when the controller boots, full screen | Must |
| FR-14 | The About page shows the HB logo, and its Exit Application button closes the application after confirmation, leaving the machine running | Should |
| FR-15 | The About page's Shut Down Controller button, after confirmation, has the PLC save its persistent settings and shut the controller down | Should |

### Non-Functional Requirements

| ID | Requirement | Priority |
|----|-------------|----------|
| NFR-01 | With no on-screen button held, the machine behaves identically with the application running, closed, crashed or unable to connect, and the physical buttons always work | Must |
| NFR-02 | Out of scope: the loss of the screen while one of its buttons is held is not detected (see FR-08) | Won't |
| NFR-03 | Running the application causes no PLC task overruns on the controller | Must |
| NFR-04 | A change of machine status appears on screen within half a second | Should |
| NFR-05 | Status is readable from the operator's position at the table, and is never shown by colour alone | Should |
| NFR-06 | Buttons and settings work with the screen's input device alone. If it is a touch screen, without a physical keyboard | Should |
| NFR-07 | Every press and release from the screen is logged as coming from the screen | Should |
| NFR-08 | Any PLC additions follow the HB TwinCAT 3 Structured Text coding standard | Must |
| NFR-09 | The whole application fits the 1024×768 panel without clipping | Must |

## Technical Constraints

- Windows desktop application (WPF) running on the CX51x0 controller and talking to the PLC
  locally over ADS. Confirm the controller's operating system and resources can run it next to
  the 10 ms PLC task.
- A monitor or touch panel has to be sourced and fitted to the controller's display port. It must
  be mounted where the operator can see the table while holding Forward or Reverse.
- The on-screen buttons are operational controls, not safety functions. The safety reset from
  the screen takes the same route through the standard PLC as the physical Reset, which is
  acceptable for this safety system. Guards and the E-stop remain the protection.
- Touch screens can drop a touch mid-hold. A dropped touch counts as a release, so the move stops
  with the self-clearing early-release error.
- Persistent settings are saved automatically when the controller shuts down. The built-in UPS
  makes sure this happens on a power loss.

## Success Criteria

- [x] Each status (safety stop, error, stopped before dwell, moving, ready) appears in the top bar as the PLC's text when the machine is put into it (FR-01)
- [x] The PLC's message is shown and matches the online view (FR-02)
- [x] The screen shows at-station status, matching the dwell cam (FR-03)
- [x] Every success criterion of Hold-to-Run Manual Index and Resume After Early Release passes using the on-screen Forward and Reverse (FR-04, FR-07)
- [x] On-screen Reset clears a latched fault and, after an E-stop, resets the safety system (FR-05)
- [x] During a move, presses of other buttons, on screen or physical, have no effect, and the move continues while its button is held on screen or physically (FR-06)
- [x] Killing the application while an on-screen Forward is held lets the index run on to the station; the next Reset clears the held button, and the physical buttons then work as before (FR-08)
- [x] Index speed and both ramps can be changed from the screen, and the next index uses them. Changing one form of a ramp updates the other (FR-09)
- [x] An out-of-range value cannot be entered on the screen, and a value the PLC refuses goes back to the value in force (FR-10)
- [x] A value set on the screen is still in force after a power cycle (FR-11)
- [x] A value changed online in XAE appears on the screen (FR-12)
- [x] After a power cycle, the application comes up on its own and shows the machine status once the PLC runs (FR-13)
- [x] The About page shows the HB logo; Exit Application asks, then closes the application with the machine still running (FR-14)
- [x] Shut Down Controller asks, then the PLC saves its settings and the controller shuts down (FR-15)
- [x] With the application closed, the physical buttons work exactly as before (NFR-01)
- [x] The PLC task shows no overruns with the application running (NFR-03)
- [x] The log shows on-screen presses and releases as coming from the screen (NFR-07)
- [x] Every tab fits the 1024×768 panel without clipping (NFR-09)

## Verification Plan

1. **Bench:** run the application against the PLC and step the machine through each status. With
   the table safe, enter good and bad values.
2. **On site, with guards and E-stop in force:** fit the screen and power-cycle to confirm
   auto-start. Repeat the hold-to-run and creep tests with the on-screen buttons. Mix screen and
   physical buttons during moves. Kill the application during a held move, then Reset. Watch the
   PLC task's cycle time and overruns.
3. **Hand-over:** William drives the table and changes the speed and ramps from the screen, and
   confirms it tells him what he needs.

## Out of Scope

* On-screen jog, continuous rotation or commanded (not held) indexes
* Engineer settings: pre-stop enable, index timeout, direction inversion, maximum velocity
* User log-in or access levels
* Alarm history, trends, recipes or production data
* Remote access, a web HMI or TwinCAT HMI
* The full production HMI for the automatic system
* Detecting the loss of the screen while one of its buttons is held (a heartbeat or watchdog)
* Showing on screen why the PLC refused a setting, until the PLC log is shown on screen
* Logging setting changes with their source
* Showing that the screen is not connected to the PLC, or that the PLC is not running

## Known Limitations

- Anyone at the screen can move the table and change its settings. Guards and the E-stop are the
  protection, and open access is acceptable in the lab.
- A setting changed just before the controller fails without a clean shutdown may be lost, since
  persistent settings are saved at shutdown.
- The screen is only as informative as the PLC's status messages.
- If the screen loses the PLC, the top bar does not say so.
- A Forward or Reverse left held by a screen that closed or crashed holds its move to the station,
  and that pushbutton gives no fresh press until Reset clears it.
- An on-screen button is released when the finger lifts, not when it slides off the button.
- A ramp inside the screen's limits can still be refused by the PLC once scaled to the index speed;
  it goes back to the value in force with no reason shown.

## References

PLC repo PRDs, in [HB1971A_PLC `dev-docs/PRDs`](https://github.com/HuxleyBertram/HB1971A_PLC/tree/dev/dev-docs/PRDs):

* [PRD: Resume After Early Release](https://github.com/HuxleyBertram/HB1971A_PLC/blob/dev/dev-docs/PRDs/cf7d6e53-resume-after-early-release.md):
  the stopped-before-dwell status and the continue behaviour the on-screen buttons share
* [PRD: Adjustable Drive Ramps](https://github.com/HuxleyBertram/HB1971A_PLC/blob/dev/dev-docs/PRDs/30d5322a-adjustable-drive-ramps.md):
  the ramp settings shown here
* [PRD: Hold-to-Run Manual Index](https://github.com/HuxleyBertram/HB1971A_PLC/blob/dev/dev-docs/PRDs/d962f879-hold-to-run-manual-index.md):
  the button behaviour replicated on screen
* [PRD: Rotary Table Manual Index](https://github.com/HuxleyBertram/HB1971A_PLC/blob/dev/dev-docs/PRDs/b72db9c7-rotary-table-manual-index.md):
  table status, faults and settings

Other sources:

* Trial at the machine with William, the week of 2026-10-12, and a later voice note
