# Tabline Mac companion

Development edition for macOS 14 or later, on Apple Silicon and Intel. The existing Chrome extension uses this companion for the Midnight Glass rope, desktop window discovery, clipboard copying and PNG saving. Windows continues using its existing companion.

## Install a test build

1. Open the latest successful [Mac build](https://github.com/Qasimhassan65/tabline/actions/workflows/macos-companion.yml?query=branch%3Amacos-companion). Download the `arm64` artifact for Apple Silicon or `x86_64` for Intel. GitHub requires sign-in to download workflow artifacts.
2. Extract the downloaded artifact, then extract the development ZIP inside it. Keep `Install.command` beside `Tabline Companion.app`.
3. Load the extension folder in Chrome at `chrome://extensions`, using Developer mode and Load unpacked. Open Tabline's options and copy its extension ID.
4. Quit Chrome and any previous Tabline companion before updating the companion. Open `Install.command`, confirm installation, and follow the setup window. The app is copied to your own `Applications` folder; an existing app is preserved as a dated backup.
5. Paste the extension ID and choose **Connect Chrome extension**. Choose **Allow Screen Recording** to include desktop app windows. Approve the permission in System Settings and restart Chrome if macOS requests it.
6. Quit the standalone setup app, reopen Chrome, and use **Option + S**. If Chrome reserves that shortcut, assign another in `chrome://extensions/shortcuts`. Click a card to copy, or hover and choose Save.

These development builds are ad-hoc signed, not Apple-notarized. If Gatekeeper blocks opening, use macOS's per-app Open Anyway approval only after verifying the downloaded build and source. Never disable Gatekeeper globally. Public release requires Developer ID signing and notarization.

Screen Recording is required for desktop apps. Accessibility is optional and used only to restore a minimized window when direct capture fails. Chrome tab copying works without desktop permission once the companion is connected. A denied permission does not trigger repeated prompts while opening the rope.

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

Build output is under `companion-macos/dist/`. End users do not need Swift, Node or Python. The native host registration is stored only for the current user at `~/Library/Application Support/Google/Chrome/NativeMessagingHosts/com.tabline.companion.json`. Moving the app requires reconnecting it in setup.

For public distribution, build `universal`, configure a Developer ID Application identity and a notarytool keychain profile, then run `notarize.sh` with `TABLINE_SIGN_IDENTITY` and `TABLINE_NOTARY_PROFILE` set. Credentials must stay outside the repository. A Chrome developer account does not provide Apple signing credentials.

## Test and remove

Follow `docs/MACOS-TESTING.md` in the repository for the interactive acceptance checks. Automated compilation, geometry/cache tests and the native handshake do not validate desktop capture permission, visual layout or clipboard pasting in real apps.

To disconnect, quit Chrome and run `Uninstall.command`. It removes only the registration pointing to this installation. You can then drag the companion app to Trash and revoke its permissions in System Settings. Saved screenshots are left intact.
