# Testing Tabline on macOS

Use an interactive Mac running macOS 14 or later with Google Chrome. You can use a borrowed Mac or an interactive hosted Mac; confirm that the provider allows Screen Recording permission, clipboard use and app installation before paying. A headless CI runner verifies builds and the native protocol, but cannot replace these checks.

## Set up

Follow `companion-macos/README.md`. Installing or loading the extension unpacked must automatically open Mac setup and request one universal installer download. Open the disk image, choose Install Tabline, then use Connect Tabline on the browser setup page. Confirm the native connection request and approve screen access. Verify the page switches to Ready automatically. There must be no processor choice, copied ID or manual connection-check requirement.

Begin with desktop Screen Recording denied. Opening Tabline must return to the existing setup tab without duplicate tabs or repeated downloads. Grant permission using the companion setup window and restart Chrome only if requested. Then the rope must work and desktop cards must appear. Accessibility can remain off for ordinary covered-window capture. Repeat setup with an unpacked extension whose ID differs from the first installation, and confirm both permitted IDs remain registered after explicit native approval.

## Acceptance checklist

| Check | Expected result |
| --- | --- |
| Open with Option + S | Transparent overlay above the browser content; current tab centered with blue outline; no debugger notice. |
| Move and scroll | Smooth curve, stable card positions, hover outline and Copy/Save controls; clicks remain responsive during motion. |
| Arrow navigation | Next/previous card glides into place; arrows disable at the correct ends; targets remain usable in narrow windows. |
| Copy active Chrome tab | Rope closes immediately; paste using Command + V into Notes or an image editor; success appears only after clipboard writing. |
| Copy another loaded tab | Selected tab is activated and captured; the pasted image belongs to that tab. |
| Copy unvisited/discarded tab | Site identity appears before a thumbnail exists; click still works after the page renders, or reports a clear loading failure. |
| Copy a covered app window | Fresh screenshot of that app; the rope is absent from the output; no unrelated window is substituted. |
| Minimized app | Capture succeeds if available; otherwise restore manually, or explicitly grant optional Accessibility and retry. |
| Closed app or denied permission | Failure toast; no success and no screenshot of another app. Reopen the rope to refresh app discovery. |
| Save | Choose destination and inspect PNG dimensions and image quality; confirm saved toast. Cancel another save and confirm no success toast/file. |
| Restricted Chrome page | Chrome capture first; native fallback requires screen permission and valid viewport information. Unavailable measurements must fail clearly. |
| Retina and external monitor | Correct browser alignment, complete screenshot dimensions, centered toast and accurate Copy/Save hit targets. |
| Narrow/wide/full-screen browser | No cut-off actions; arrows usable; card sizes adapt; try macOS Spaces and an external display. |
| Dismiss | Escape and outside click dismiss; changing to another application dismisses; no invisible overlay remains. |
| Reopen repeatedly | No duplicate app cards, accumulating timers, persistent capture streams or rising idle CPU. |
| Reduce Motion | macOS Reduce Motion removes animated scroll/hover transitions without affecting capture. |
| Reconnect/update | Quit Chrome, update companion, restart and check connection; moving the app requires registration again. |

Use harmless test windows and images. Clipboard tests replace the current clipboard. Check both light and dark webpages behind the rope and test missing thumbnails as well as ready images.

## Measure responsiveness and resources

Record the time from click until the PNG is actually available to paste, separately from toast fade-in. Test an already loaded current tab, a second loaded tab, and a covered app. An unloaded tab and a permission prompt necessarily take longer; do not promise zero-latency capture.

In Activity Monitor inspect Tabline Companion with the rope closed, open without interaction, and scrolling. Motion timers should stop after settling and thumbnail work should stop when closed. Memory includes AppKit, screenshot buffers and icons in addition to the explicit thumbnail budgets. Repeat 30 open/copy/close cycles and check that memory stabilizes. The `stats` native action reports compressed thumbnail bytes, capture count, card count and motion state; `frameBytes` stays zero because full screenshot history is not retained.

## Verification boundaries

The GitHub workflow builds and tests Apple Silicon and Intel separately, packages each app, verifies ad-hoc signatures, and checks framed native messages, capability reporting, failure responses and shutdown. It does not grant privacy permission or inspect personal screen content.

Before public release, complete the interactive checklist on an actual Mac, sign and notarize the app with your Apple Developer ID, and verify installation on a clean Mac user account. Test with the final Chrome Web Store extension ID; development IDs can differ. This branch is a development implementation until those checks are recorded.
