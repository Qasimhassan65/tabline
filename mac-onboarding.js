// Mac setup is required on installation. No background polling or OS permission prompts.
const MacOnboarding = (() => {
  const platform = chrome.runtime.getPlatformInfo();
  let opening;
  async function open(autoDownload = false) {
    if (opening) return opening;
    opening = (async () => {
      const base = chrome.runtime.getURL('mac-setup.html');
      const existing = (await chrome.tabs.query({})).find(tab => !tab.incognito && tab.url?.split('?')[0] === base);
      if (existing) {
        await chrome.tabs.update(existing.id, {active:true});
        if (existing.windowId !== undefined) await chrome.windows.update(existing.windowId, {focused:true});
        return;
      }
      await chrome.tabs.create({url:base + (autoDownload ? '?install=1' : '')});
    })().finally(() => {opening = null;});
    return opening;
  }
  async function installed() {
    if ((await platform).os !== 'mac') return;
    try {
      const status = await nativeRequest('ping', {}, 2000);
      if (status.platform === 'mac' && status.permissions?.screenRecording === true) return;
    } catch {}
    await open(true);
  }
  async function beforeOpen() {
    if ((await platform).os !== 'mac') return true;
    try {
      const status = await nativeRequest('ping', {}, 2000);
      if (status.platform === 'mac' && status.permissions?.screenRecording === true) return true;
    } catch {}
    await open(true);
    return false;
  }
  return {open, installed, beforeOpen};
})();
