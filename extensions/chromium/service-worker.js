import { nativeConnection, sendImage, TIMING } from "./protocol.js";
import { imageContext, acquireImage } from "./acquire.js";
let current = null;
async function sourceCurrent(context, op) {
  const check = () => { if (op.abort.signal.aborted || current !== op) throw new Error("原操作已失效"); };
  check();
  const latest = await chrome.tabs.get(context.tabId);
  check();
  if (latest.id !== context.tabId || latest.windowId !== context.windowId || !latest.active || latest.url !== context.pageUrl) throw new Error("原页面或窗口已改变");
  // A native desktop confirmation may have focus. Bind the last browser window,
  // without requiring focused=true or retrieving any other tabs or page content.
  const window = await chrome.windows.getLastFocused();
  check();
  if (window.id !== context.windowId) throw new Error("原浏览器窗口已改变");
}
async function status(text, error = false) {
  await chrome.action.setBadgeText({ text: error ? "!" : "" });
  await chrome.action.setTitle({ title: `快译：${text}` });
}
chrome.runtime.onInstalled.addListener(() => {
  chrome.contextMenus.create({ id: "kuaiyi-image", title: "快译", contexts: ["image"], documentUrlPatterns: ["https://*/*"] }, () => {
    if (chrome.runtime.lastError) void status("无法建立图片菜单", true);
  });
});
chrome.contextMenus.onClicked.addListener(async (info, tab) => {
  current?.abort.abort();
  const operationId = crypto.randomUUID(), abort = new AbortController(), op = { operationId, abort, tabId: tab?.id, windowId: tab?.windowId };
  current = op; const timer = setTimeout(() => abort.abort(), TIMING.total); let connection = null, bytes = null;
  const clearBytes = () => bytes?.fill(0);
  abort.signal.addEventListener("abort", clearBytes, { once: true });
  try {
    const context = imageContext(info, tab);
    connection = nativeConnection(chrome.runtime.connectNative("com.kuaiyi.native_host"), operationId, abort.signal, Date.now() + TIMING.total, () => abort.abort());
    // Desktop authorization before any image byte read/capture. Off/unregistered/untrusted cannot enter acquisition.
    await connection.exchange({ type: "OfferImage", operationId }, "Ready");
    await sourceCurrent(context, op);
    bytes = await acquireImage(context, abort.signal);
    await sourceCurrent(context, op);
    await sendImage(connection.exchange, operationId, bytes, abort.signal);
    if (current === op) await status("图片已交付本机；翻译受桌面开关和端点授权控制");
  } catch (error) {
    abort.abort(); if (current === op) await status(`${error.message}。请使用桌面快译的区域截图快捷键，由您明确选框`, true);
  } finally {
    clearTimeout(timer); clearBytes(); abort.signal.removeEventListener("abort", clearBytes); connection?.close(); if (current === op) current = null;
  }
});
chrome.tabs.onRemoved.addListener(id => { if (current?.tabId === id) current.abort.abort(); });
chrome.tabs.onActivated.addListener(info => { if (current?.windowId === info.windowId && current.tabId !== info.tabId) current.abort.abort(); });
chrome.tabs.onUpdated.addListener((id, changes) => { if (current?.tabId === id && (changes.status === "loading" || changes.url)) current.abort.abort(); });
chrome.tabs.onDetached.addListener(id => { if (current?.tabId === id) current.abort.abort(); });
chrome.tabs.onAttached.addListener(id => { if (current?.tabId === id) current.abort.abort(); });
chrome.windows.onRemoved.addListener(id => { if (current?.windowId === id) current.abort.abort(); });
// WINDOW_ID_NONE (-1) also occurs while the explicit native confirmation has focus.
chrome.windows.onFocusChanged.addListener(id => { if (current && id >= 0 && id !== current.windowId) current.abort.abort(); });
chrome.action.onClicked.addListener(() => { current?.abort.abort(); void chrome.runtime.openOptionsPage(); });
