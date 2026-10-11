export const LIMIT = 256 * 1024, CHUNK = 190 * 1024, IMAGE = 20 * 1024 * 1024;
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
export function base64(bytes) {
  let text = "";
  for (let i = 0; i < bytes.length; i += 8192) text += String.fromCharCode(...bytes.subarray(i, i + 8192));
  return btoa(text);
}
export async function sendImage(exchange, operationId, bytes, signal) {
  pngSize(bytes);
  const check = () => { if (signal.aborted) throw new Error("已取消"); };
  check();
  const hash = [...new Uint8Array(await crypto.subtle.digest("SHA-256", bytes))].map(n => n.toString(16).padStart(2, "0")).join("");
  check();
  await exchange(checkedFrame({ type: "BeginImage", transferId: operationId, totalLength: bytes.length, mime: "image/png", sha256: hash }), "Begun");
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
export function nativeConnection(port, operationId, signal, expires) {
  let waiter = null, closed = false;
  const reject = message => { if (waiter) { const old = waiter; waiter = null; clearTimeout(old.timer); old.reject(new Error(message)); } };
  const abort = () => {
    reject("已取消");
    if (!closed) { try { port.postMessage({ type: "CancelImage", operationId }); } catch { /* Port already disconnected; no transfer can survive host EOF. */ } port.disconnect(); closed = true; }
  };
  port.onDisconnect.addListener(() => { closed = true; reject("本机宿主失联，请检查桌面安装与扩展授权"); });
  port.onMessage.addListener(message => {
    if (!waiter) { if (!closed) port.disconnect(); closed = true; return; }
    const old = waiter; waiter = null; clearTimeout(old.timer);
    if (message?.type === "Error") old.reject(new Error(`快译拒绝：${message.code}`));
    else if (message?.type !== old.expected || message.operationId !== operationId || (message.type === "Ready" && message.protocol !== 1)) old.reject(new Error("宿主响应身份或版本不匹配"));
    else old.resolve(message);
  });
  signal.addEventListener("abort", abort, { once: true });
  return {
    exchange(message, expected) {
      if (closed || signal.aborted || waiter || Date.now() >= expires) return Promise.reject(new Error("操作已失效"));
      return new Promise((resolve, fail) => {
        const timer = setTimeout(() => { reject("本次操作超时"); abort(); }, Math.min(5000, expires - Date.now()));
        waiter = { resolve, reject: fail, expected, timer };
        try { port.postMessage(checkedFrame(message)); } catch { reject("无法发送到本机宿主"); abort(); }
      });
    },
    close() { signal.removeEventListener("abort", abort); reject("连接已结束"); if (!closed) port.disconnect(); closed = true; }
  };
}
