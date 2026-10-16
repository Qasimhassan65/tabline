# Tabline 2.2 · Midnight Glass

A transparent silver cable with micro-clips and Midnight Glass cards. Sharp image previews, site/app icons, a single title footer, blue hover outline and smooth scrolling. Click any card to close the picker, switch to the selected tab/app and copy a fresh screenshot. A missing preview does not disable the card. Hover a card for Copy and Save. Save opens a PNG destination dialog; cancelling leaves no saved file or success notice. Success and failure appear in a compact bottom-center toast.

## Curved rope

The current tab starts centered and upright before the first visible frame. Browser cards follow the cable's shallow curve with up to 1.8 degrees of tilt, pivoting at the clip fastener. Images, labels, hover outlines and buttons rotate together. Clips stay seated on the cable. The same scroll position controls translation, elevation and tilt, retaining the existing elapsed-time easing without bounce or automatic tab activation.

Desktop app cards follow the same curved path and retain their live DWM previews. Their complete surfaces stay upright because DWM thumbnail destinations do not support rotation; no extra screenshot stream or preview cache is introduced to simulate it. Source order remains stable during preview refreshes. The first and last cards can reach the center, and resizing preserves the source at the center. Drawing and Copy/Save hit testing share one transform, including display scaling. Motion timers still stop when scrolling settles or the picker closes.

On opening, a blue outline identifies the centered current tab without showing Copy/Save until hover. Hovering a preview replaces this opening outline; scrolling or using an arrow clears it for the rest of that picker session. After that, only the hovered preview is blue.

Fixed Midnight Glass arrow buttons move to the next or previous card position using the existing smooth animation. Repeated clicks accumulate, the appropriate arrow dims and becomes inactive at each end, and arrows are hidden for a single source. Clear edge gutters keep native images and hover actions away from the controls. Below 360 logical pixels of window width, the arrows sit below the previews to preserve image size. The clickable targets are 44 logical pixels across. Existing mouse-wheel behavior over cards is unchanged; there is no global scroll hook or background input listener.

## Update / install

Existing project: reload Tabline in `chrome://extensions`. This also disconnects the previous helper and stops its old background capture jobs. Refresh any webpage still showing an older injected rope. Press Alt+S.

New install: load this folder unpacked in Chrome, copy the extension ID from its options page, run `companion/Install.cmd` with that ID. The host is registered for this Windows user; no service or startup task is installed. Reinstall the host registration after moving the project folder. Change the shortcut in `chrome://extensions/shortcuts` if necessary.

## Capture model

- Chrome: activate the selected tab and its window, wait for a rendered page, capture the visible viewport as an original-resolution PNG and copy. Already loaded pages only need a brief paint opportunity. A discarded/unloaded page necessarily needs to load. A wrong active tab or navigation race produces a failure rather than copying another tab.
- Restricted Chrome page: try Chrome capture first; if access is blocked, the companion copies the visible foreground Chrome viewport. It verifies the Chrome process/title before capture. A known viewport size is reused when possible; unusual Chrome toolbar/DPI configurations may affect the fallback crop.
- Desktop app: restore if minimized, bring it forward, allow the desktop compositor to present it, then copy visible pixels at original resolution using GDI. There is no WGC stream or yellow WGC border. Another app stealing focus causes failure; another always-on-top window can appear in this ordinary visible-screen capture.
- Clipboard copying is confirmed before the success toast. Failure produces a red cross and "Screenshot failed" ; the extension action title holds the failure reason. Windows foreground restrictions or protected surfaces can still reject capture.

## Previews and resource policy

Pending captures show a delayed Capturing/Saving capsule after 350 ms; completion is reported only after clipboard/file writing succeeds. Already-active loaded tabs skip the tab-switch paint wait, and the PNG data goes directly to the companion without a redundant decode/re-encode.

Only compressed last-seen thumbnails are cached, not full-size screenshot frames. Tab thumbnails fit 360×220 at JPEG quality 62. They are updated once after a tab visit/load and after explicit capture; repeated typing/scrolling/content changes are not tracked. Disable these visit thumbnails in settings if desired; screenshot clicks still work. At most 64 tab thumbnails, capped at 4 MiB with conservative string accounting; app thumbnails from explicit screenshots are capped at 1 MiB/32 windows. Session/process close clears these caches. Only explicitly saved PNG screenshots are written to disk; there is no automatic screenshot history.

Desktop app images use DWM thumbnail relationships, reusing Windows' existing compositor surfaces while the rope is open. Only visible cards receive thumbnail hosts, and hosts are disposed when scrolled off-screen or the rope closes. There are no screenshot jobs to generate these previews, and no app-focus hooks, app warm pass, periodic app capture or permanently running capture stream. Some apps may provide no compositor image; their source card with its icon, name, domain/app name and Capture on click remains available.

The native rope is an alpha-layered window; each app preview uses a separate ordinary, owned tool window because DWM thumbnails require a compositor destination. These hosts never appear in the taskbar and do not take focus. Cards keep their positions during thumbnail updates. Scrolling uses a 15 ms timer only during motion, an elapsed-time easing curve and accumulated wheel input. The input-dismissal timer runs only while the rope is visible. Toast timers stop after dismissal.

Site icons come from Chrome's local favicon service; desktop icons come from the owning executable where Windows makes them available. No remote icon service is used. The glass appearance uses native gradient surfaces, edge highlights and transparency, rather than a continuous backdrop-blur capture pass.

Cache limits exclude runtime, metadata, transient full-resolution capture buffers, compositor resources and the Windows clipboard. No upload, account, telemetry, debugger permission, registry capture-border override or taskbar progress API is used.

## Rebuild and validation

`companion/Build.ps1` uses the Windows .NET Framework C# compiler. This edition needs no C++ DLL, Node or Python runtime for normal use. `npm run check` checks extension JavaScript syntax.

`companion/TablineCompanion-2.2.0-navigation.exe --self-test` runs owned test windows, tests foreground screenshot copying and eased scrolling, and writes test-render PNG files beside the helper. It deliberately replaces the clipboard with a fixture screenshot and clicks only its owned test window. Test code is separate from the ordinary capture path.


## Stable baseline

The `stable-before-curvature` tag preserves the approved silver cable, micro-clips and Midnight Glass UI before any curvature work. Capture behavior is unchanged in this baseline.

Source code is tracked in Git. The exact installed Windows x64 companion is attached to the matching [stable release](https://github.com/Qasimhassan65/tabline/releases/tag/stable-before-curvature). After checking out the stable tag, place `TablineCompanion-2.2.0-rope.exe` inside `companion/`, then run `companion/Install.cmd` with your unpacked Chrome extension ID to use that baseline without rebuilding. Otherwise the installer builds the companion for the checked-out version from source automatically. Re-run the installer when updating to the curved edition so registration points to its new companion executable.

Personal screenshots, generated native-host registration and older build outputs are excluded from the repository. Each installation generates its own registration.

To return to this source version on a separate local branch:

```sh
git switch -c restored-stable stable-before-curvature
```

For an exact executable rollback, also use the companion attached to that stable release.
