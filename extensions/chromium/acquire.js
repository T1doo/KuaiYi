import { IMAGE, pngSize } from "./protocol.js";
export function imageContext(info, tab) {
  if (info.menuItemId !== "kuaiyi-image" || info.mediaType !== "image" || !Number.isInteger(tab?.id) || !tab.active || (info.frameId ?? 0) !== 0) throw new Error("当前 frame 无法安全定位图片，请使用快译区域截图");
  const page = new URL(tab.url), clicked = new URL(info.pageUrl), image = new URL(info.srcUrl);
  if (page.protocol !== "https:" || page.href !== clicked.href || page.username || page.password || !["https:", "blob:", "data:"].includes(image.protocol) || image.username || image.password) throw new Error("页面或图片来源受限，请使用快译区域截图");
  return { tabId: tab.id, windowId: tab.windowId, pageUrl: page.href, imageUrl: image.href, origin: page.origin };
}
export async function original(context, signal) {
  const check = () => { if (signal.aborted) throw new ImageAcquisitionError("Cancelled", "已取消"); };
  check();
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
    if (!response.ok || response.headers.get("content-type")?.split(";")[0].trim() !== "image/png") throw new Error("原图不可取得或不是 PNG");
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
    pngSize(bytes);
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
