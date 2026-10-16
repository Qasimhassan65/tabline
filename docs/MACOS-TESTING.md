# Mac Chrome-only testing (2.4.0)

1. Download and extract Tabline-extension.zip. In chrome://extensions enable Developer mode, choose Load unpacked and select the extracted folder containing manifest.json. Reload an existing installation if replacing it.
2. Check the extension version is 2.4.0. No installer or setup page should open. You can quit the old Tabline Companion if installed.
3. Open two ordinary websites. On the first, press Option + S. The transparent Midnight Glass rope should appear over the page, with the current tab highlighted.
4. Scroll using the trackpad, mouse wheel or arrows. Hover a card: Copy and Save should appear. Escape and clicking outside dismiss the rope. Repeating the shortcut toggles it closed.
5. Select the second tab's card, including a card without a thumbnail. It should activate that tab, dismiss the rope, capture and show Screenshot copied. Paste into an image-capable app with Command + V. The image should include neither rope nor toast.
6. Reopen and choose Save. Choose a PNG location. Screenshot saved should appear after saving completes. Cancel a save: no success notice. Reopen while a prior toast is visible and capture again: the prior toast must not enter the screenshot.
7. Try a newly loaded or discarded tab: allow it to finish loading. A closed tab, changed page or protected surface should produce Screenshot failed, rather than the wrong image.
8. On chrome://extensions press Option + S. A compact tab picker should open, never settings or the old installer. Select a regular site to copy it. Chrome may refuse a screenshot of a protected page itself.
9. Try a narrow and a wide Chrome window. Cards and arrows should remain usable and scrolling should settle without an idle animation loop.
10. Optional settings should say Chrome tabs only on Mac. No connection check or Screen Recording permission should be required.

If Option + S is intercepted by the remote viewer or another app, click the Tabline toolbar icon. Check chrome://extensions/shortcuts inside the Mac browser and assign Option + S there if needed. A remote testing provider can independently prohibit extension loading, clipboard sharing or downloads; this is separate from Tabline's capture logic.

Windows regression: Alt + S must still open the existing native rope with app windows and tabs, with Copy/Save unchanged.

Implementation verification on Windows Chrome with the Mac route selected in a temporary test copy: real clipboard PNG (1725 × 1063), real completed PNG download, clickable missing-thumbnail card, Escape dismissal, 420-pixel layout and protected-page picker passed. This does not replace checking Option + S and clipboard paste on physical or remote macOS.
