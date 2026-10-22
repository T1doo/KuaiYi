export const LIMIT = 256 * 1024, CHUNK = 190 * 1024, IMAGE = 20 * 1024 * 1024;
export const TIMING = Object.freeze({ handshake: 5000, confirmation: 30000, transfer: 30000, total: 65000 });
export const RASTER_MIMES = Object.freeze(["image/png", "image/jpeg", "image/bmp", "image/webp", "image/gif"]);
export function checkedFrame(message) {
  if (new TextEncoder().encode(JSON.stringify(message)).length > LIMIT) throw new Error("消息超过字节限制");
  return message;
}
export function pngSize(bytes) {
  const signature = [137, 80, 78, 71, 13, 10, 26, 10];
  if (!(bytes instanceof Uint8Array) || bytes.length < 33 || bytes.length > IMAGE || !signature.every((n, i) => bytes[i] === n)) throw new Error("P0 仅接受有限 PNG 图片，请使用区域截图");
  const d = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  if (d.getUint32(8) !== 13 || String.fromCharCode(...bytes.slice(12, 16)) !== "IHDR") throw new Error("图片头无效");
  const width = d.getUint32(16), height = d.getUint32(20);
  if (!width || !height || width > 16384 || height > 16384 || width * height > 24000000) throw new Error("图片尺寸超限");
  return { width, height };
}
// Only identify the bounded original container here. No browser decode, canvas,
// DOM dimensions or metadata rewriting. The host checks MIME/canvas and the
// isolated Worker enforces the existing format subsets before making a preview.
export function encodedMime(bytes) {
  if (!(bytes instanceof Uint8Array) || !bytes.length || bytes.length > IMAGE) throw new Error("图片字节超限或无效");
  const text = (at, count) => String.fromCharCode(...bytes.subarray(at, at + count));
  if (bytes[0] === 137) { pngSize(bytes); return "image/png"; }
  if (bytes.length >= 4 && bytes[0] === 255 && bytes[1] === 216 && bytes.at(-2) === 255 && bytes.at(-1) === 217) return "image/jpeg";
  if (bytes.length >= 54 && text(0, 2) === "BM") return "image/bmp";
  if (bytes.length >= 13 && ["GIF87a", "GIF89a"].includes(text(0, 6))) return "image/gif";
  if (bytes.length >= 20 && text(0, 4) === "RIFF" && text(8, 4) === "WEBP") return "image/webp";
  throw new Error("不支持的图片字节，请使用区域截图");
}
export function base64(bytes) {
  let text = "";
  for (let i = 0; i < bytes.length; i += 8192) text += String.fromCharCode(...bytes.subarray(i, i + 8192));
  return btoa(text);
}
export async function sendImage(exchange, operationId, bytes, signal) {
  const mime = encodedMime(bytes);
  const check = () => { if (signal.aborted) throw new Error("已取消"); };
  check();
  const hash = [...new Uint8Array(await crypto.subtle.digest("SHA-256", bytes))].map(n => n.toString(16).padStart(2, "0")).join("");
  check();
  await exchange(checkedFrame({ type: "BeginImage", transferId: operationId, totalLength: bytes.length, mime, sha256: hash }), "Begun");
  let index = 0;
  for (let offset = 0; offset < bytes.length; offset += CHUNK) {
    check();
    const current = index++;
    const response = await exchange(checkedFrame({ type: "Chunk", transferId: operationId, index: current, data: base64(bytes.subarray(offset, offset + CHUNK)) }), "ChunkAccepted");
    if (response.index !== current) throw new Error("宿主分片确认不匹配");
  }
  check();
  await exchange({ type: "CommitImage", transferId: operationId }, "Committed");
}
// One acknowledged frame outstanding at a time; native port disconnect and absolute operation deadline reject the waiter.
export function nativeConnection(port, operationId, signal, expires, onTerminal = () => {}) {
  // Caller supplies an epoch deadline; all elapsed stage checks use the monotonic browser clock.
  const now = () => performance.now(), totalExpires = now() + Math.min(TIMING.total, Math.max(0, expires - Date.now()));
  let waiter = null, closed = false, phase = "initial", transferExpires = null, transferTimer = null, lifetimeTimer;
  const clearLife = () => { clearTimeout(lifetimeTimer); clearTimeout(transferTimer); };
  const reject = message => { if (waiter) { const old = waiter; waiter = null; clearTimeout(old.timer); old.reject(new Error(message)); } };
  const abort = () => {
    reject("已取消");
    if (!closed) { try { port.postMessage({ type: "CancelImage", operationId }); } catch { /* Port already disconnected; no transfer can survive host EOF. */ } closed = true; clearLife(); port.disconnect(); onTerminal(); }
  };
  lifetimeTimer = setTimeout(() => { reject("本次操作超时"); abort(); }, Math.max(0, totalExpires - now()));
  port.onDisconnect.addListener(() => { const notify = !closed; closed = true; clearLife(); reject("本机宿主失联，请检查桌面安装与扩展授权"); if (notify) onTerminal(); });
  port.onMessage.addListener(message => {
    if (closed) return;
    if (!waiter) { abort(); return; }
    if (now() >= waiter.expires) { reject("本次操作超时"); abort(); return; }
    if (message?.type === "ConfirmationPending") {
      if (phase !== "offering" || waiter.expected !== "Ready" || message.operationId !== operationId || message.protocol !== 1 || message.timeoutMs !== TIMING.confirmation ||
          Object.keys(message).sort().join(",") !== "operationId,protocol,timeoutMs,type") {
        reject("宿主确认阶段身份或版本不匹配"); abort(); return;
      }
      phase = "confirming"; clearTimeout(waiter.timer);
      waiter.expires = Math.min(totalExpires, now() + TIMING.confirmation);
      waiter.timer = setTimeout(() => { reject("等待桌面确认超时"); abort(); }, waiter.expires - now());
      return;
    }
    const old = waiter; waiter = null; clearTimeout(old.timer);
    if (message?.type === "Error") { old.reject(new Error(`快译拒绝：${message.code}`)); abort(); }
    else if (message?.type !== old.expected || message.operationId !== operationId || (message.type === "Ready" && message.protocol !== 1)) { old.reject(new Error("宿主响应身份或版本不匹配")); abort(); }
    else {
      if (message.type === "Ready") { phase = "transferring"; transferExpires = Math.min(totalExpires, now() + TIMING.transfer); transferTimer = setTimeout(() => { reject("传图阶段超时"); abort(); }, transferExpires - now()); }
      else if (message.type === "Committed") phase = "completed";
      old.resolve(message);
    }
  });
  signal.addEventListener("abort", abort, { once: true });
  return {
    exchange(message, expected) {
      const offer = message.type === "OfferImage" && expected === "Ready";
      if (closed || signal.aborted || waiter || now() >= totalExpires || (offer ? phase !== "initial" : phase !== "transferring") ||
          (transferExpires !== null && now() >= transferExpires)) return Promise.reject(new Error("操作已失效"));
      if (offer) phase = "offering";
      return new Promise((resolve, fail) => {
        const until = Math.min(totalExpires, transferExpires ?? totalExpires, now() + TIMING.handshake);
        const timer = setTimeout(() => { reject("本次操作超时"); abort(); }, until - now());
        waiter = { resolve, reject: fail, expected, timer, expires: until };
        try { port.postMessage(checkedFrame(message)); } catch { reject("无法发送到本机宿主"); abort(); }
      });
    },
    close() { signal.removeEventListener("abort", abort); reject("连接已结束"); const notify = !closed; closed = true; clearLife(); if (notify) { port.disconnect(); onTerminal(); } }
  };
}
