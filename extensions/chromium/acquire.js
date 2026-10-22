import { IMAGE, RASTER_MIMES, encodedMime } from "./protocol.js";
const BASE64_LIMIT = 4 * Math.ceil(IMAGE / 3), DATA_URL_LIMIT = BASE64_LIMIT + 23, DATA_BLOCK = 64 * 1024;
const invalidData = () => new ImageAcquisitionError("InvalidDataImage", "内嵌图片的类型、编码或大小无效");
const checkCancelled = signal => { if (signal.aborted) throw new ImageAcquisitionError("Cancelled", "已取消"); };
function sextet(c) {
  if (c >= 65 && c <= 90) return c - 65;
  if (c >= 97 && c <= 122) return c - 71;
  if (c >= 48 && c <= 57) return c + 4;
  return c === 43 ? 62 : c === 47 ? 63 : -1;
}
async function embeddedOriginal(raw, signal) {
  checkCancelled(signal);
  if (typeof raw !== "string" || raw.length > DATA_URL_LIMIT) throw invalidData();
  const mime = RASTER_MIMES.find(type => raw.startsWith(`data:${type};base64,`));
  if (!mime) throw invalidData();
  const start = `data:${mime};base64,`.length, length = raw.length - start, end = raw.length;
  if (!length || length > BASE64_LIMIT || length % 4 !== 0) throw invalidData();
  const padding = raw.charCodeAt(end - 1) === 61 ? (raw.charCodeAt(end - 2) === 61 ? 2 : 1) : 0;
  const size = length / 4 * 3 - padding, dataEnd = end - padding;
  if (size <= 0 || size > IMAGE) throw invalidData();
  let result = null, transferred = false;
  const clear = () => result?.fill(0);
  const checkpoint = async () => {
    checkCancelled(signal);
    await new Promise(resolve => setTimeout(resolve, 0)); // Let real navigation/stop events revoke the original task.
    checkCancelled(signal);
  };
  signal.addEventListener("abort", clear, { once: true });
  try {
    // Validate the complete canonical alphabet/padding before allocating any decoded bytes.
    for (let at = start; at < end; at += DATA_BLOCK) {
      const stop = Math.min(end, at + DATA_BLOCK);
      for (let i = at; i < stop; i++) if (i < dataEnd ? sextet(raw.charCodeAt(i)) < 0 : raw.charCodeAt(i) !== 61) throw invalidData();
      await checkpoint();
    }
    if (padding && (sextet(raw.charCodeAt(dataEnd - 1)) & (padding === 2 ? 15 : 3)) !== 0) throw invalidData();
    checkCancelled(signal);result = new Uint8Array(size);checkCancelled(signal);let output = 0;
    // No atob string, browser decode or temporary pixel buffer; the original task owns this single array.
    for (let at = start; at < end; at += DATA_BLOCK) {
      const stop = Math.min(end, at + DATA_BLOCK);
      for (let i = at; i < stop; i += 4) {
        const a = sextet(raw.charCodeAt(i)), b = sextet(raw.charCodeAt(i + 1)), c = sextet(raw.charCodeAt(i + 2)), d = sextet(raw.charCodeAt(i + 3));
        result[output++] = a << 2 | b >> 4;
        if (output < size) result[output++] = (b & 15) << 4 | c >> 2;
        if (output < size) result[output++] = (c & 3) << 6 | d;
      }
      await checkpoint();
    }
    if (output !== size || result.length > IMAGE || encodedMime(result) !== mime) throw invalidData();
    checkCancelled(signal);transferred = true;return result;
  } catch (error) {
    if (error?.code === "Cancelled") throw error;
    throw invalidData(); // Never let a data URL or decoder detail enter status/log text.
  } finally {
    signal.removeEventListener("abort", clear);
    if (!transferred) clear();
  }
}
export function imageContext(info, tab) {
  if (info.menuItemId !== "kuaiyi-image" || info.mediaType !== "image" || !Number.isInteger(tab?.id) || tab.id < 0 || !Number.isInteger(tab.windowId) || tab.windowId < 0 || !tab.active || (info.frameId ?? 0) !== 0) throw new Error("当前 frame 无法安全定位图片，请使用快译区域截图");
  if (typeof info.srcUrl !== "string" || info.srcUrl.length > DATA_URL_LIMIT) throw invalidData();
  const embedded = info.srcUrl.startsWith("data:"), page = new URL(tab.url), clicked = new URL(info.pageUrl), image = embedded ? null : new URL(info.srcUrl);
  if (page.protocol !== "https:" || page.href !== clicked.href || page.username || page.password || image && (!["https:", "blob:"].includes(image.protocol) || image.username || image.password)) throw new Error("页面或图片来源受限，请使用快译区域截图");
  // Keep embedded bytes literal: URL parsing must not strip whitespace, fragments or normalize payload syntax.
  return { tabId: tab.id, windowId: tab.windowId, pageUrl: page.href, imageUrl: embedded ? info.srcUrl : image.href, origin: page.origin };
}
export async function original(context, signal) {
  const check = () => { if (signal.aborted) throw new ImageAcquisitionError("Cancelled", "已取消"); };
  check();
  if (typeof context.imageUrl !== "string" || context.imageUrl.length > DATA_URL_LIMIT) throw invalidData();
  if (context.imageUrl.startsWith("data:")) return await embeddedOriginal(context.imageUrl, signal);
  const url = new URL(context.imageUrl);
  if (url.protocol !== "https:" || url.origin !== context.origin) throw new Error("原图不在当前临时授权范围");
  const response = await fetch(url.href, { credentials: "omit", redirect: "error", referrerPolicy: "no-referrer", cache: "no-store", signal });
  const reader = response.body?.getReader(); if (!reader) throw new Error("原图没有可读字节");
  const parts = [], errors = []; let count = 0, result = null, cancellation = null;
  // Keep the original cancellation outcome until cleanup joins it; never abandon a reader promise.
  const cancel = () => cancellation ??= Promise.resolve().then(() => reader.cancel()).then(() => null, error => ({ error }));
  const clear = () => { parts.forEach(part => part.fill(0)); result?.fill(0); };
  const onAbort = () => { clear(); void cancel(); };
  signal.addEventListener("abort", onAbort, { once: true });
  try {
    check();
    const mime = response.headers.get("content-type")?.split(";")[0].trim().toLowerCase();
    if (!response.ok || !RASTER_MIMES.includes(mime)) throw new Error("原图不可取得或不是受支持图片");
    const declared = response.headers.get("content-length");
    if (declared && (!/^\d+$/.test(declared) || Number(declared) > IMAGE)) throw new Error("原图过大");
    while (true) {
      check();
      const { done, value } = await reader.read();
      if (value) parts.push(value); // Own even a late or oversized chunk before any rejection.
      check();
      if (done) break;
      count += value.length; if (count > IMAGE) throw new Error("原图过大");
    }
    const bytes = result = new Uint8Array(count); let offset = 0;
    for (const part of parts) { bytes.set(part, offset); offset += part.length; part.fill(0); }
    if (encodedMime(bytes) !== mime) throw new Error("图片声明类型与原始字节不符");
  } catch (error) { errors.push(error); }
  finally {
    parts.forEach(part => part.fill(0));
    if (errors.length) result?.fill(0);
    const cleanup = await cancel();
    if (cleanup) errors.push(cleanup.error);
    signal.removeEventListener("abort", onAbort);
    try { reader.releaseLock(); } catch (error) { errors.push(error); }
  }
  if (signal.aborted && !errors.some(error => error?.code === "Cancelled")) errors.push(new ImageAcquisitionError("Cancelled", "已取消"));
  if (errors.length) {
    result?.fill(0);
    throw errors.length === 1 ? errors[0] : new AggregateError(errors, "图片读取及清理失败");
  }
  return result;
}
// Numeric bounds utility only; a rectangle is not evidence of image pixel ownership.
export function cropPixels(rect, width, height) {
  if (!rect || ![rect.x, rect.y, rect.width, rect.height, rect.viewportWidth, rect.viewportHeight, rect.dpr, width, height].every(Number.isFinite) || rect.width <= 0 || rect.height <= 0 || rect.viewportWidth <= 0 || rect.viewportHeight <= 0 || rect.dpr <= 0 || rect.dpr > 8 || Math.abs(width - rect.viewportWidth * rect.dpr) > 1 || Math.abs(height - rect.viewportHeight * rect.dpr) > 1) throw new Error("截图缩放或尺寸无法验证");
  const x = Math.ceil(rect.x * rect.dpr), y = Math.ceil(rect.y * rect.dpr), right = Math.floor((rect.x + rect.width) * rect.dpr), bottom = Math.floor((rect.y + rect.height) * rect.dpr);
  if (x < 0 || y < 0 || right > width || bottom > height || right <= x || bottom <= y || right - x > 16384 || bottom - y > 16384 || (right - x) * (bottom - y) > 24000000) throw new Error("图片裁切范围无效");
  return { x, y, width: right - x, height: bottom - y };
}
export class ImageAcquisitionError extends Error {
  constructor(code, message) { super(message); this.name = "ImageAcquisitionError"; this.code = code; }
}
export async function visibleCrop() {
  // There is no complete proof of arbitrary occlusion/CSS or atomic tab-to-capture binding in this implementation.
  // Reject before any DOM injection, viewport capture, decode or byte transfer, even for apparently simple images.
  throw new ImageAcquisitionError("ScreenshotOwnershipUnverified", "浏览器图片截图暂不可用：不能可靠证明全部像素属于目标图片");
}
export async function acquireImage(context, signal) {
  try { return await original(context, signal); }
  catch {
    if (signal.aborted) throw new ImageAcquisitionError("Cancelled", "已取消");
    return await visibleCrop();
  }
}
