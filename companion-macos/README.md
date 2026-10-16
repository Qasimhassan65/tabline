# Tabline Mac companion

Development edition for macOS 14 or later, on Apple Silicon and Intel. The existing Chrome extension uses this companion for the Midnight Glass rope, desktop window discovery, clipboard copying and PNG saving. Windows continues using its existing companion.

## Automatic Mac setup

1. Install the Chrome extension or load its folder unpacked. On Mac, Tabline automatically opens its setup page and requests one installer download per extension version. If Chrome blocks that download, use the visible Download installer button.
2. Open `Tabline-Mac.dmg`, open Tabline Companion, and choose **Install Tabline**. One universal installer supports both Apple Silicon and Intel. The app installs into your user Applications folder and opens the installed copy.
3. On the Chrome setup page, choose **Connect Tabline**. Approve opening the Mac app, then choose **Connect & allow screen access** in its window. The extension ID is passed automatically; no copying, terminal or architecture selection is needed.
4. Approve Screen Recording in macOS System Settings. Restart Chrome only if requested. The setup page detects completion automatically; no Check connection action is required. Use **Option + S** on a webpage.

Mac setup is the required first-run flow, including for unpacked extensions. A ready Mac installation skips the installer on updates. Repeated shortcut presses focus the existing setup tab, and automatic download is attempted at most once per version. Connection checks run only while the setup page is visible, for at most three minutes; there is no permanent onboarding poll.

The direct download is a public development prerelease at [Mac setup preview](https://github.com/Qasimhassan65/tabline/releases/tag/macos-setup-preview). GitHub Actions artifacts remain available for developer validation, but are not part of the normal setup flow.

These development builds are ad-hoc signed, not Apple-notarized. If Gatekeeper blocks opening, use macOS's per-app Open Anyway approval only after verifying the downloaded build and source. Never disable Gatekeeper globally. Public release requires Developer ID signing and notarization.

Screen Recording is required to complete Mac setup and include desktop apps. Accessibility is optional and used only to restore a minimized window when direct capture fails. Denying permission leaves setup visible without repeatedly showing OS permission prompts. The browser capture engine itself does not need desktop permission, but this edition requires completing desktop setup before opening the Mac rope.

## Preview and capture behavior

- Chrome previews are compressed last-seen images, with the existing 4 MiB extension budget. An unvisited tab shows its real site icon, title, domain and Capture on click; selecting it activates and loads the tab before a fresh capture.
- Desktop previews are snapshots requested only for visible cards while the rope is open, with at most two preview jobs and a 1 MiB compressed cache. They do not continuously track app changes. The decoded image cache is capped at 3 MiB; visible cards, icons, metadata and transient capture buffers are additional memory.
- Clicking a desktop card dismisses the rope immediately and requests a fresh ScreenCaptureKit screenshot at native pixel density. Covered windows can be captured directly. A minimized or unavailable window may need manual restoration, or optional Accessibility permission. Protected content and closed windows can fail.
- No debugger attachment, screen-recording stream, background capture loop, account, cloud upload or screenshot history is used. macOS may display its own screen-access indicators or permission reminders.
- Copy writes a PNG to the Mac clipboard. Save opens the standard destination dialog. Cancelling does not report a successful save. Only an explicitly saved screenshot is written to disk.
- Screenshot dimensions are bounded to 16000 pixels per edge and 32 million pixels to prevent oversized allocations. Clipboard images remain in the OS clipboard until replaced.

## Build from source on a Mac

Install Xcode command line tools, then run from the repository root:

```sh
swift test --package-path companion-macos
bash companion-macos/build.sh arm64
# Intel: use x86_64 instead. Both architectures in one app: use universal.
python3 tests/native-host-smoke.py 'companion-macos/dist/arm64/Tabline Companion.app/Contents/MacOS/TablineCompanion'
```

Build output is under `companion-macos/dist/`; building `universal` also creates `Tabline-Mac.dmg`. End users do not need Swift, Node or Python. User-level host manifests are registered for Google Chrome, Chrome for Testing and Chromium. Moving the app requires reconnecting it in setup. Registration requires explicit confirmation in the native app; visiting a `tabline://connect` link alone does not authorize an extension.

For public distribution, build `universal`, configure a Developer ID Application identity and a notarytool keychain profile, then run `notarize.sh` with `TABLINE_SIGN_IDENTITY` and `TABLINE_NOTARY_PROFILE` set. Credentials must stay outside the repository. A Chrome developer account does not provide Apple signing credentials.

## Test and remove

Follow `docs/MACOS-TESTING.md` in the repository for the interactive acceptance checks. Automated compilation, geometry/cache tests and the native handshake do not validate desktop capture permission, visual layout or clipboard pasting in real apps.

To disconnect, quit Chrome and run `Uninstall.command`. It removes only the registration pointing to this installation. You can then drag the companion app to Trash and revoke its permissions in System Settings. Saved screenshots are left intact.
