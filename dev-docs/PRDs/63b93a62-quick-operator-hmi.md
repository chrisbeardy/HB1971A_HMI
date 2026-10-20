---
name: Quick Operator HMI
status: proposed
date: 2026-10-19
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

**Status.** The screen shows one overall machine status, in large text with a matching colour, so
it can be read from the table. It also shows the PLC's plain-text message saying what the table
is doing or what is wrong and what to press:

- Starting up, or not connected to the PLC
- Safety stop: E-stop pressed or a guard open, then reset the safety system
- Error: the reason, and press Reset
- Stopped before dwell: hold Forward or Reverse to continue
- Moving
- Ready, and whether the table is at a station

**Buttons.** On-screen Forward, Reverse and Reset do exactly what the physical buttons do.
Forward and Reverse are hold-to-run, one station per press, and continue after an early release.
Reset clears faults and resets the safety system. The screen and physical buttons act as one
set: the button that started a move has to stay held, and every other button, on screen or
physical, is ignored until the move ends. If the screen goes away while one of its buttons is
held (the application closes or crashes, or its connection drops), the PLC treats that as the
button being let go.

**Settings.** The operator can change the index speed and both ramps, each ramp in both of its
linked forms. The PLC checks every change. A refused value is shown as refused, with the reason,
and the screen goes back to the value in force. Accepted changes are kept like the other table
settings, saved automatically when the controller shuts down.

**Independence.** With no on-screen button held, the machine runs exactly the same whether the
screen is running, closed or crashed. The physical buttons always work.

## Requirements

### Functional Requirements

| ID | Requirement | Priority |
|----|-------------|----------|
| FR-01 | The screen shows one overall machine status: starting up or not connected, safety stop, error, stopped before dwell, moving, or ready | Must |
| FR-02 | The screen shows the PLC's plain-text status or fault message, including what the operator needs to press | Must |
| FR-03 | The screen shows whether the table is at a station | Should |
| FR-04 | On-screen Forward and Reverse behave exactly like the physical buttons: hold-to-run, one station per press, a fresh press needed for each move, and continuing after an early release | Must |
| FR-05 | On-screen Reset behaves exactly like the physical Reset, including resetting the safety system | Must |
| FR-06 | On-screen and physical buttons act as one set. The button that started a move must stay held, and every other button, on screen or physical, is ignored until the move ends | Must |
| FR-07 | An on-screen button counts as released when the finger or pointer lifts off it or leaves it | Must |
| FR-08 | If the application closes or crashes, or its connection to the PLC is lost, while an on-screen button is held, the PLC treats the button as released | Must |
| FR-09 | The operator can view and change the index speed, the acceleration and the deceleration, each ramp in both its drive-native and time-to-index-speed forms, linked as in Adjustable Drive Ramps | Must |
| FR-10 | The PLC validates every change from the screen. A refused value is shown as refused with its reason, and the screen reverts to the value in force | Must |
| FR-11 | A setting changed on the screen survives a power cycle, through the same automatic save at shutdown as the other persistent settings | Must |
| FR-12 | Values changed elsewhere, such as online in XAE, appear on the screen | Should |
| FR-13 | The application starts by itself when the controller boots, full screen, and shows starting up or not connected until the PLC is reachable and running | Must |

### Non-Functional Requirements

| ID | Requirement | Priority |
|----|-------------|----------|
| NFR-01 | With no on-screen button held, the machine behaves identically with the application running, closed, crashed or unable to connect, and the physical buttons always work | Must |
| NFR-02 | The loss of the screen while one of its buttons is held is detected and acted on as a release within half a second | Must |
| NFR-03 | Running the application causes no PLC task overruns on the controller | Must |
| NFR-04 | A change of machine status appears on screen within half a second | Should |
| NFR-05 | Status is readable from the operator's position at the table, and is never shown by colour alone | Should |
| NFR-06 | Buttons and settings work with the screen's input device alone. If it is a touch screen, without a physical keyboard | Should |
| NFR-07 | Every press, release and setting change from the screen is logged as coming from the screen | Should |
| NFR-08 | Any PLC additions follow the HB TwinCAT 3 Structured Text coding standard | Must |

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

- [ ] Each status (starting up or not connected, safety stop, error, stopped before dwell, moving, ready) appears correctly when the machine is put into it (FR-01)
- [ ] The PLC's message is shown and matches the online view (FR-02)
- [ ] The screen shows at-station status, matching the dwell cam (FR-03)
- [ ] Every success criterion of Hold-to-Run Manual Index and Resume After Early Release passes using the on-screen Forward and Reverse (FR-04, FR-07)
- [ ] On-screen Reset clears a latched fault and, after an E-stop, resets the safety system (FR-05)
- [ ] During a move held on screen, the physical buttons and the other on-screen button have no effect, and the same applies the other way round (FR-06)
- [ ] Killing the application, and separately pulling its connection, while an on-screen button is held stops the table within half a second with the early-release error (FR-08, NFR-02)
- [ ] Index speed and both ramps can be changed from the screen, and the next index uses them. Changing one form of a ramp updates the other (FR-09)
- [ ] An out-of-range value entered on the screen is refused with its reason, and the screen shows the value in force (FR-10)
- [ ] A value set on the screen is still in force after a power cycle (FR-11)
- [ ] A value changed online in XAE appears on the screen (FR-12)
- [ ] After a power cycle, the application comes up on its own and shows starting up until the PLC runs (FR-13)
- [ ] With the application closed, the physical buttons work exactly as before (NFR-01)
- [ ] The PLC task shows no overruns with the application running (NFR-03)
- [ ] The log shows on-screen presses, releases and setting changes as coming from the screen (NFR-07)

## Verification Plan

1. **Bench:** run the application against the PLC and step the machine through each status. With
   the table safe, enter good and bad values.
2. **On site, with guards and E-stop in force:** fit the screen and power-cycle to confirm
   auto-start. Repeat the hold-to-run and creep tests with the on-screen buttons. Mix screen and
   physical buttons during moves. Kill the application, and separately disconnect it, during a
   held move. Watch the PLC task's cycle time and overruns.
3. **Hand-over:** William drives the table and changes the speed and ramps from the screen, and
   confirms it tells him what he needs.

## Out of Scope

* On-screen jog, continuous rotation or commanded (not held) indexes
* Engineer settings: pre-stop enable, index timeout, direction inversion, maximum velocity
* User log-in or access levels
* Alarm history, trends, recipes or production data
* Remote access, a web HMI or TwinCAT HMI
* The full production HMI for the automatic system

## Known Limitations

- Anyone at the screen can move the table and change its settings. Guards and the E-stop are the
  protection, and open access is acceptable in the lab.
- A setting changed just before the controller fails without a clean shutdown may be lost, since
  persistent settings are saved at shutdown.
- The screen is only as informative as the PLC's status messages.

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
