import { nativeConnection, sendImage } from "./protocol.js";
import { imageContext, acquireImage } from "./acquire.js";
let current = null;
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
  current = op; const timer = setTimeout(() => abort.abort(), 30000); let connection = null, bytes = null;
  try {
    const context = imageContext(info, tab);
    connection = nativeConnection(chrome.runtime.connectNative("com.kuaiyi.native_host"), operationId, abort.signal, Date.now() + 30000);
    // Desktop authorization before any image byte read/capture. Off/unregistered/untrusted cannot enter acquisition.
    await connection.exchange({ type: "OfferImage", operationId }, "Ready");
    bytes = await acquireImage(context, abort.signal);
    const latest = await chrome.tabs.get(context.tabId);
    if (!latest.active || latest.url !== context.pageUrl || current !== op) throw new Error("原操作已失效");
    await sendImage(connection.exchange, operationId, bytes, abort.signal);
    if (current === op) await status("图片已交付本机；翻译受桌面开关和端点授权控制");
  } catch (error) {
    abort.abort(); if (current === op) await status(`${error.message}。请使用您明确选框的截图；本构建的区域截图链路尚未接通`, true);
  } finally {
    clearTimeout(timer); bytes?.fill(0); connection?.close(); if (current === op) current = null;
  }
});
chrome.tabs.onRemoved.addListener(id => { if (current?.tabId === id) current.abort.abort(); });
chrome.tabs.onActivated.addListener(info => { if (current?.windowId === info.windowId && current.tabId !== info.tabId) current.abort.abort(); });
chrome.tabs.onUpdated.addListener((id, changes) => { if (current?.tabId === id && (changes.status === "loading" || changes.url)) current.abort.abort(); });
chrome.action.onClicked.addListener(() => { current?.abort.abort(); void chrome.runtime.openOptionsPage(); });
