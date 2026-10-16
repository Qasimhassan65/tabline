const INSTALLER_URL = 'https://github.com/Qasimhassan65/tabline/releases/download/macos-setup-preview/Tabline-Mac.dmg';
const $ = selector => document.querySelector(selector);
const version = chrome.runtime.getManifest().version;
$('#download').href = INSTALLER_URL;
$('#connect').href = `tabline://connect?extension=${encodeURIComponent(chrome.runtime.id)}`;
let timer, checking = false, closed = false, attempts = 0;
let deadline = Date.now() + 3 * 60 * 1000;
const hostDetected = result => result.ok || /access to.*native messaging host.*forbidden/i.test(result.error || '');
function pause() { clearTimeout(timer); }
function resume() {attempts = 0; deadline = Date.now() + 3 * 60 * 1000; check();}
async function check() {
  pause();
  if (checking || closed || document.hidden || attempts >= 60 || Date.now() > deadline) return;
  checking = true; attempts++;
  try {
    const result = await chrome.runtime.sendMessage({type:'COMPANION_STATUS', setup:true});
    if (closed) return;
    const connected = result.ok && result.platform === 'mac';
    $('#install-step').classList.toggle('complete', hostDetected(result));
    $('#connect-step').classList.toggle('complete', connected);
    $('#permissions').hidden = !connected || result.permissions?.screenRecording === true;
    if (!connected) {
      $('#connection-status').textContent = hostDetected(result) ? 'Tabline is installed. Choose Connect Tabline to link this Chrome installation.' : 'Open the installer, then connect Tabline. This page will detect it automatically.';
    } else if (result.permissions?.screenRecording !== true) {
      $('#connection-status').textContent = 'Connected. Allow Screen Recording to finish setting up desktop screenshots. Restart Chrome only if macOS requests it.';
    } else {
      $('#ready-step').classList.add('complete'); $('#ready-heading').textContent = 'Your rope is ready';
      $('#connection-status').textContent = 'Press Option + S on a webpage to capture a tab or app.';
      $('#continue').hidden = false;
      await chrome.storage.local.set({macSetupComplete:true});
      return;
    }
    $('#continue').hidden = true; $('#ready-step').classList.remove('complete'); $('#ready-heading').textContent = 'We’ll connect automatically';
  } catch { $('#connection-status').textContent = 'Open Tabline Companion to finish setup.'; }
  finally { checking = false; }
  if (!closed && !document.hidden && attempts < 60 && Date.now() < deadline) timer = setTimeout(check, 2500);
}
$('#download').addEventListener('click', () => {$('#download-status').textContent = 'Open Tabline-Mac.dmg from Chrome’s downloads, then open Tabline Companion.';});
$('#connect').addEventListener('click', () => {$('#connection-status').textContent = 'Approve opening Tabline, then choose Connect & allow screen access in its window.'; resume();});
$('#permissions').onclick = async () => {await chrome.runtime.sendMessage({type:'COMPANION_SETUP'});};
$('#continue').onclick = () => chrome.tabs.create({url:'https://www.google.com/'});
document.addEventListener('visibilitychange', () => {if (document.hidden) pause(); else resume();});
window.addEventListener('focus', resume);
window.addEventListener('pagehide', () => {closed = true; pause();});
(async () => {
  // Let the welcome page finish loading before initiating a file navigation.
  if (document.readyState !== 'complete') await new Promise(resolve => window.addEventListener('load', resolve, {once:true}));
  const result = await chrome.runtime.sendMessage({type:'COMPANION_STATUS', setup:true}).catch(() => ({ok:false}));
  const stored = await chrome.storage.local.get('macInstallerAttempt');
  if (!hostDetected(result) && new URLSearchParams(location.search).has('install') && stored.macInstallerAttempt !== version) {
    // Request one download per version. A visible link remains if Chrome blocks automatic downloading.
    await chrome.storage.local.set({macInstallerAttempt:version});
    $('#download').click();
  }
  await check();
})().catch(() => {$('#connection-status').textContent = 'Use Download installer to begin setup.';});
