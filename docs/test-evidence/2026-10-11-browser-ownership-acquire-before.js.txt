import { IMAGE, pngSize } from "./protocol.js";
export function imageContext(info, tab) {
  if (info.menuItemId !== "kuaiyi-image" || info.mediaType !== "image" || !Number.isInteger(tab?.id) || !tab.active || (info.frameId ?? 0) !== 0) throw new Error("当前 frame 无法安全定位图片，请使用快译区域截图");
  const page = new URL(tab.url), clicked = new URL(info.pageUrl), image = new URL(info.srcUrl);
  if (page.protocol !== "https:" || page.href !== clicked.href || page.username || page.password || !["https:", "blob:", "data:"].includes(image.protocol) || image.username || image.password) throw new Error("页面或图片来源受限，请使用快译区域截图");
  return { tabId: tab.id, windowId: tab.windowId, pageUrl: page.href, imageUrl: image.href, origin: page.origin };
}
export async function original(context, signal) {
  const url = new URL(context.imageUrl);
  if (url.protocol !== "https:" || url.origin !== context.origin) throw new Error("原图不在当前临时授权范围");
  const response = await fetch(url.href, { credentials: "omit", redirect: "error", referrerPolicy: "no-referrer", cache: "no-store", signal });
  if (!response.ok || response.headers.get("content-type")?.split(";")[0].trim() !== "image/png") throw new Error("原图不可取得或不是 PNG");
  const declared = response.headers.get("content-length");
  if (declared && (!/^\d+$/.test(declared) || Number(declared) > IMAGE)) throw new Error("原图过大");
  const reader = response.body?.getReader(); if (!reader) throw new Error("原图没有可读字节");
  const parts = []; let count = 0, result = null;
  try {
    while (true) { const { done, value } = await reader.read(); if (done) break; count += value.length; if (count > IMAGE) throw new Error("原图过大"); parts.push(value); }
    const bytes = result = new Uint8Array(count); let offset = 0;
    for (const part of parts) { bytes.set(part, offset); offset += part.length; part.fill(0); }
    pngSize(bytes); return bytes;
  } catch (error) { result?.fill(0); throw error; }
  finally { try { await reader.cancel(); } finally { parts.forEach(part => part.fill(0)); } }
}
// Executed by scripting in an isolated world, only for this explicit operation. No webpage message listener.
export function measureImage(imageUrl, operationId) {
  const candidates = [...document.images].filter(image => (image.currentSrc || image.src) === imageUrl);
  if (candidates.length !== 1 || !candidates[0].complete) return null;
  const element = candidates[0], style = getComputedStyle(element), r = element.getBoundingClientRect(), v = visualViewport;
  if (!element.naturalWidth || style.visibility !== "visible" || style.display === "none" || Number(style.opacity) !== 1 || style.transform !== "none" || v?.scale !== 1 || v.offsetLeft !== 0 || v.offsetTop !== 0) return null;
  const x = Math.max(0, r.left), y = Math.max(0, r.top), right = Math.min(innerWidth, r.right), bottom = Math.min(innerHeight, r.bottom);
  if (right <= x || bottom <= y || devicePixelRatio <= 0 || devicePixelRatio > 8) return null;
  // Center must hit this element; reject overlays, uncertain clipped ancestors and transformed containers.
  if (document.elementFromPoint((x + right) / 2, (y + bottom) / 2) !== element) return null;
  for (let p = element.parentElement; p; p = p.parentElement) { const s = getComputedStyle(p); if (s.transform !== "none" || Number(s.opacity) !== 1 || [s.overflowX, s.overflowY].some(a => ["hidden", "clip", "scroll", "auto"].includes(a))) return null; }
  return { operationId, x, y, width: right - x, height: bottom - y, left: r.left, top: r.top, right: r.right, bottom: r.bottom, scrollX, scrollY, dpr: devicePixelRatio, viewportWidth: innerWidth, viewportHeight: innerHeight, page: location.href };
}
export function cropPixels(rect, width, height) {
  if (!rect || ![rect.x, rect.y, rect.width, rect.height, rect.viewportWidth, rect.viewportHeight, rect.dpr, width, height].every(Number.isFinite) || rect.width <= 0 || rect.height <= 0 || rect.viewportWidth <= 0 || rect.viewportHeight <= 0 || rect.dpr <= 0 || rect.dpr > 8 || Math.abs(width - rect.viewportWidth * rect.dpr) > 1 || Math.abs(height - rect.viewportHeight * rect.dpr) > 1) throw new Error("截图缩放或尺寸无法验证");
  const x = Math.ceil(rect.x * rect.dpr), y = Math.ceil(rect.y * rect.dpr), right = Math.floor((rect.x + rect.width) * rect.dpr), bottom = Math.floor((rect.y + rect.height) * rect.dpr);
  if (x < 0 || y < 0 || right > width || bottom > height || right <= x || bottom <= y || right - x > 16384 || bottom - y > 16384 || (right - x) * (bottom - y) > 24000000) throw new Error("图片裁切范围无效");
  return { x, y, width: right - x, height: bottom - y };
}
export async function visibleCrop(api, context, operationId, signal) {
  const measure = async () => {
    const tab = await api.tabs.get(context.tabId);
    if (!tab.active || tab.windowId !== context.windowId || tab.url !== context.pageUrl || signal.aborted) throw new Error("页面已改变");
    const results = await api.scripting.executeScript({ target: { tabId: context.tabId, frameIds: [0] }, func: measureImage, args: [context.imageUrl, operationId] });
    if (results.length !== 1 || results[0].frameId !== 0 || results[0].result?.operationId !== operationId || results[0].result.page !== context.pageUrl) throw new Error("不能唯一确认可见目标");
    return results[0].result;
  };
  const before = await measure();
  let data = await api.tabs.captureVisibleTab(context.windowId, { format: "png" });
  const after = await measure();
  if (JSON.stringify(before) !== JSON.stringify(after) || signal.aborted) throw new Error("截图期间图片位置或缩放已改变");
  if (!data.startsWith("data:image/png;base64,") || data.length > 64 * 1024 * 1024) throw new Error("截图格式或字节限额无效");
  const binary = atob(data.slice(22)); data = "";
  const pixels = Uint8Array.from(binary, c => c.charCodeAt(0));
  const bitmap = await createImageBitmap(new Blob([pixels], { type: "image/png" })); pixels.fill(0);
  try {
    const rect = cropPixels(before, bitmap.width, bitmap.height);
    const canvas = new OffscreenCanvas(rect.width, rect.height);
    try {
      const draw = canvas.getContext("2d"); if (!draw) throw new Error("无法裁切图片");
      draw.drawImage(bitmap, rect.x, rect.y, rect.width, rect.height, 0, 0, rect.width, rect.height);
      const blob = await canvas.convertToBlob({ type: "image/png" });
      if (blob.size > IMAGE || signal.aborted) throw new Error("裁切图片过大或操作已取消");
      const bytes = new Uint8Array(await blob.arrayBuffer()); pngSize(bytes); return bytes;
    } finally { canvas.width = canvas.height = 1; }
  } finally { bitmap.close(); }
}
