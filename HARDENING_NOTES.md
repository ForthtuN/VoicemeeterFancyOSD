# VoicemeeterFancyOSD hardening notes

This document tracks the fork-specific fixes and validation work completed during the 1.2.2.2 hardening effort. It is intended both as a user-facing summary and as source material for future upstream pull requests.

The focused hardening work starts with commit `d8f8911` (`Keep OSD render window topmost when shown`). Older commits already present on the popup test branch are not claimed here as part of this hardening pass.

## 1. Intermittent OSD disappearance / popup Z-order

### Problem

FancyOSD could stop being visibly rendered in the upper-right even though the normal update path was still running.

Failure-time diagnostics showed that:

- Voicemeeter dirty notifications still arrived.
- `UpdateOsd()` and the normal show path still executed.
- both the outer window and render `HwndSource` still existed;
- Windows still reported the render window as visible, uncloaked and topmost.

A live `SetWindowPos(..., HWND_TOPMOST, ...)` call on the render HWND immediately restored the OSD, confirming that the real failure was effective Z-order placement rather than a stopped notification/update pipeline.

### Fix

- Preserve the Windows 10/11 popup architecture introduced by 1.2.2.2.
- After repositioning the render `HwndSource`, explicitly reassert `HWND_TOPMOST` using `SetWindowPos` with no activation, no move/resize and `SWP_SHOWWINDOW`.
- Follow with `ShowWindow(..., ShowNoActivate)`.

This avoids reverting to the older child-window approach while directly addressing the confirmed failure mode.

## 2. BandWindow / Win32 lifecycle hardening

- Removed unsafe/reentrant window-destruction behavior from the native window message path.
- Added runtime fallback from `CreateWindowInBand` to `CreateWindowEx` if the band-window creation path fails.
- Hardened Win32 style mutation helpers so critical `Get/SetWindowLongPtr` failures are checked instead of silently ignored.
- Cleaned up Unicode/exact P/Invoke declarations, including explicit wide-character entry points where appropriate.
- Added `WM_NCDESTROY` lifecycle cleanup for `WndProcHookManager` registrations.
- Fixed the static `WndProcHookManager` registry so destroyed windows do not remain strongly rooted forever.
- Added safe same-instance destroy/recreate support.
- Clear HWND-scoped message hooks before rebuilding them on a recreated HWND, preventing duplicate-key registration failures.
- Re-register the object only after successful native HWND creation, avoiding a static-root leak if recreate fails before `WM_NCDESTROY` can ever occur.
- Kept `WM_NCDESTROY` unregister timing after hook/default-window-proc dispatch so the manager is still available while the final message is being processed.

## 3. Voicemeeter polling and shutdown concurrency

- Prevent overlapping timer callbacks with an interlocked polling ownership guard.
- Harden shutdown so logout ownership is handed off safely between the UI/shutdown path and an in-flight polling callback.
- Prevent new polls after shutdown begins.
- Avoid blocking the UI thread waiting for a poll that may itself need to dispatch to the UI.
- Fix failed initialization so a failed `LoadAsync()` does not leave the wrapper permanently looking initialized.
- Extract polling-rate mapping into `PollingRatePolicy`.
- Fix `VeryFast` so it actually maps to 140 Hz instead of falling back to 30 Hz.
- Preserve stable enum numeric values/config compatibility:
  - Slow = 15 Hz
  - Normal = 30 Hz
  - Fast = 60 Hz
  - VeryFast = 140 Hz
  - unknown/fallback = 30 Hz

## 4. Configuration storage and file-watcher hardening

- Removed normal read/save dependence on one shared mutable `IniData` instance.
- Each read/save now operates on its own local `IniData` object.
- Added atomic config writes using a unique temporary file in the same directory followed by overwrite move/replace.
- Serialize the final file-replacement operation under a short write lock.
- Ensure watcher pause state is restored in `finally` for synchronous and asynchronous saves.
- Properly await the inner async work dispatched through WPF `Dispatcher.InvokeAsync`.
- Add `Created` and `Renamed` watcher handling in addition to `Changed`.
- Include `NotifyFilters.FileName` so editors that save via rename/replace are detected.
- Add `AsyncDebouncer` so bursty watcher notifications collapse to the latest reload and reloads execute serially.
- Preserve `SaveData()` semantics without making normal reads/writes depend again on shared mutable parser state.

## 5. Logger concurrency and shutdown

- Corrected the channel configuration for multiple writers instead of declaring a single writer while logging from several threads.
- Retain the logger processing task instead of leaving it fully fire-and-forget.
- Complete the writer and deterministically drain pending log entries during disposal.
- Keep logger disposal late enough in application shutdown that other exit handlers can still write their final messages.

## 6. Updater and archive-install hardening

- Make optional copy-progress reporting null-safe.
- Fix installation of files inside nested directories.
- Preserve/install empty directories where required.
- Stage update contents before replacing the active installation.
- Use deterministic staged-install ordering.
- Harden backup creation and deletion ordering.
- Do not discard the last known-good backup when installation fails.
- Roll back files/directories created by a failed update.
- Add path validation against ZIP path traversal / Zip Slip.
- Ensure extracted entries cannot escape the intended destination directory.
- Improve failure handling around partial installs so update failure is much less likely to leave the application unusable.

## 7. Autostart / Windows shortcut cleanup

- Removed the old `IWshRuntimeLibrary` COM reference.
- Replaced WSH shortcut creation with direct Windows Shell Link COM interop (`IShellLinkW` + `IPersistFile`).
- Release the COM object deterministically with `Marshal.FinalReleaseComObject`.
- This also removes a local managed-build obstacle caused by the old COM reference.

## 8. Native bridge / hostfxr bootstrap hardening

- Check `LoadLibraryW` before resolving hostfxr exports.
- If required hostfxr exports are missing, unload the DLL and clear function pointers.
- Make executable-directory discovery robust beyond a fixed `MAX_PATH` buffer.
- Check `CommandLineToArgvW` failure.
- Release the command-line argv allocation with `LocalFree`.
- Replace the manually allocated .NET argument array with `std::vector`.
- Do not call `hostfxr_run_app` after failed runtime initialization.
- Close a partially created hostfxr context on initialization failure where applicable.
- Propagate the managed/runtime `hostfxr_run_app` result instead of always terminating as success.

## 9. Project/build metadata

- Ensure the managed application reports version `1.2.2.2`.
- Keep the repository native project toolset unchanged for compatibility; local validation overrides the toolset only on the development machine when needed.
- Keep the repository regression tests and GitHub workflow in the repository. They were not used as the active development validator during this local hardening pass, but they are intentionally retained.

## 10. Validation added/performed

The main development validator is intentionally local and outside the Git repository. It validates the actual working tree without requiring GitHub Actions.

Current final local verification included:

- external local regression suite: 16/16 PASS;
- repository regression suite: 7/7 PASS;
- managed x64 Release build: 0 warnings, 0 errors;
- managed x86 Release build: 0 warnings, 0 errors;
- forced native x64 bridge rebuild: PASS;
- fast BandWindow HWND regression: 100 show/hide + 10 create/destroy cycles PASS;
- full BandWindow soak: 500 show/hide + 40 create/destroy cycles PASS;
- `WndProcHookManager` count stable at 0 -> 0 after the full soak;
- process handle count stable at 245 -> 245 after the full soak;
- same-instance destroy/recreate path covered by the HWND harness;
- fast local validation completes in roughly 14 seconds on the current development machine when native outputs are already up to date;
- `git diff --check` PASS;
- multiple independent read-only code reviews found no remaining concrete introduced P0/P1/P2 issues in the final reviewed areas.

## 11. Deliberately deferred / not rewritten

These were reviewed but intentionally not turned into broad rewrites without a concrete failure motivating them:

- `DpiHelper`'s hidden-window approach;
- the larger global/static service architecture (`Globals`, static managers, etc.);
- wholesale replacement of the dual-HWND popup/render architecture.

Those remain possible future cleanup areas, but the current work deliberately prioritized reproducible bugs, race/lifetime issues and measurable validation over architectural churn.

## Suggested upstream PR split

For upstream contribution, these changes should be split into focused pull requests rather than submitted as one giant patch:

1. **Popup Z-order / BandWindow lifecycle**
   - confirmed topmost reassertion fix;
   - native window lifecycle cleanup;
   - `WndProcHookManager` lifetime/recreate fixes;
   - related Win32 declaration hardening.

2. **Voicemeeter polling / shutdown concurrency**
   - non-overlapping polling;
   - shutdown ownership handoff;
   - initialization retry behavior;
   - 140 Hz polling policy fix.

3. **Configuration watcher/storage**
   - local `IniData` operations;
   - atomic writes;
   - rename/create watcher coverage;
   - async debounce/serialization.

4. **Updater hardening**
   - staged installation;
   - rollback/backup safety;
   - nested/empty directory handling;
   - Zip Slip protection.

5. **Logger lifecycle**
   - multi-writer channel correctness;
   - deterministic draining/disposal.

6. **Autostart/build cleanup**
   - remove WSH COM reference;
   - direct Shell Link COM implementation;
   - project/build metadata cleanup.

7. **Native hostfxr bootstrap**
   - resource cleanup;
   - initialization/error handling;
   - correct run-app exit propagation.

Regression tests relevant to each area should travel with the corresponding upstream PR wherever practical.

## Relevant hardening commits already present on this branch

- `d8f8911` - Keep OSD render window topmost when shown
- `5dc37bf` - Harden FancyOSD runtime and updater
- `63dd912` - Fix Timer type ambiguity

Follow-up hardening commits on this branch:

- `a52afb5` - Harden Voicemeeter polling and shutdown
- `7c6bd18` - Harden config storage and watcher
- `d708f91` - Harden BandWindow hook lifecycle
- `1cec4b7` - Harden native bootstrap and autostart

These commits are intentionally separated by concern so they can be reviewed, cherry-picked, or used as source material for focused upstream pull requests.
