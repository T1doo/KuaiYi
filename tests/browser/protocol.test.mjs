import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { spawn } from "node:child_process";
import { once } from "node:events";
import { checkedFrame, pngSize, base64, nativeConnection, sendImage, LIMIT, CHUNK, IMAGE } from "../../extensions/chromium/protocol.js";
import { imageContext, original, acquireImage, cropPixels, visibleCrop } from "../../extensions/chromium/acquire.js";
import { ownershipCases, installDom } from "./ownership-fixtures.mjs";
const png = new Uint8Array(await readFile(new URL("../fixtures/images/kuaiyi-test.png", import.meta.url)));
const operationId = "ffffffff-ffff-ffff-ffff-ffffffffffff";
const context = { menuItemId: "kuaiyi-image", mediaType: "image", pageUrl: "https://public-fixture.invalid/page", srcUrl: "https://public-fixture.invalid/image.png", frameId: 0 };
const tab = { id: 1, windowId: 2, url: context.pageUrl, active: true };
test("manifest has exactly three used permissions and no web entry points", async () => {
  const manifest = JSON.parse(await readFile(new URL("../../extensions/chromium/manifest.json", import.meta.url)));
  assert.equal(manifest.manifest_version, 3);
  assert.deepEqual(manifest.permissions, ["contextMenus", "nativeMessaging", "activeTab"]);
  for (const field of ["host_permissions", "optional_host_permissions", "content_scripts", "externally_connectable", "web_accessible_resources", "key"]) assert.equal(manifest[field], undefined);
  const worker = await readFile(new URL("../../extensions/chromium/service-worker.js", import.meta.url), "utf8");
  assert(!worker.includes("sendNativeMessage") && !worker.includes("postMessage(") && !worker.includes("onMessage.addListener"));
});
test("context comes only from top-frame active image click", () => assert.equal(imageContext(context, tab).imageUrl, context.srcUrl));
for (const [name, change, changedTab] of [
  ["cross-frame", { frameId: 2 }], ["wrong-menu", { menuItemId: "page" }], ["text", { mediaType: "text" }],
  ["javascript", { srcUrl: "javascript:alert(1)" }], ["file", { srcUrl: "file:///fixture.png" }],
  ["userinfo", { srcUrl: "https://user:secret@public-fixture.invalid/x" }],
  ["navigated", { pageUrl: "https://public-fixture.invalid/other" }], ["inactive", {}, { ...tab, active: false }],
  ["restricted", { pageUrl: "chrome://settings" }, { ...tab, url: "chrome://settings" }]
]) test(`context rejects ${name}`, () => assert.throws(() => imageContext({ ...context, ...change }, changedTab ?? tab)));
test("UTF-8 bytes include multibyte body and JSON envelope", () => { assert.throws(() => checkedFrame({ text: "译".repeat(LIMIT / 2) })); });
test("190KiB worst base64 fits complete native frame", () => {
  const data = new Uint8Array(CHUNK).fill(255); const frame = checkedFrame({ type: "Chunk", transferId: operationId, index: 2147483647, data: base64(data) });
  assert(new TextEncoder().encode(JSON.stringify(frame)).length <= LIMIT); assert.equal(Buffer.from(frame.data, "base64").length, CHUNK);
});
test("PNG dimensions rejected before host submission", () => { const bytes = png.slice(); new DataView(bytes.buffer).setUint32(16, 16385); assert.throws(() => pngSize(bytes)); assert.throws(() => pngSize(new Uint8Array(IMAGE + 1))); });
test("original fetch never attaches credentials/referrer or follows redirects", async () => {
  const old = globalThis.fetch; let calls = 0;
  globalThis.fetch = async (url, opts) => { calls++; assert.equal(url, context.srcUrl); assert.equal(opts.credentials, "omit"); assert.equal(opts.redirect, "error"); assert.equal(opts.referrerPolicy, "no-referrer"); return new Response(png, { headers: { "content-type": "image/png" } }); };
  try { assert.deepEqual(await original(imageContext(context, tab), new AbortController().signal), png); assert.equal(calls, 1); }
  finally { globalThis.fetch = old; }
});
test("cross-origin/blob originals cannot call fetch", async () => {
  const old = globalThis.fetch; let calls = 0; globalThis.fetch = () => { calls++; throw Error("test should never fetch"); };
  try { for (const imageUrl of ["https://third-party.invalid/image.png", "blob:https://public-fixture.invalid/operation"]) await assert.rejects(original({ ...imageContext(context, tab), imageUrl }, new AbortController().signal)); assert.equal(calls, 0); }
  finally { globalThis.fetch = old; }
});
test("declared and streamed original byte limits independent", async () => {
  const old = globalThis.fetch;
  try {
    globalThis.fetch = async () => new Response(png, { headers: { "content-type": "image/png", "content-length": `${IMAGE + 1}` } });
    await assert.rejects(original(imageContext(context, tab), new AbortController().signal));
    globalThis.fetch = async () => new Response(new Uint8Array(IMAGE + 1), { headers: { "content-type": "image/png" } });
    await assert.rejects(original(imageContext(context, tab), new AbortController().signal));
  } finally { globalThis.fetch = old; }
});
const rect = { operationId, x: 2, y: 3, width: 10, height: 12, dpr: 1.5, viewportWidth: 100, viewportHeight: 100, page: context.pageUrl };
test("crop remains strictly inside target physical rectangle", () => { assert.deepEqual(cropPixels(rect, 150, 150), { x: 3, y: 5, width: 15, height: 17 }); });
for (const [name, value, width] of [["DPI", { ...rect, dpr: 0 }, 150], ["size mismatch", rect, 200], ["outside viewport", { ...rect, x: 110 }, 150], ["empty", { ...rect, width: 0 }, 150], ["NaN", { ...rect, x: NaN }, 150], ["infinite", { ...rect, width: Infinity }, 150]]) test(`crop rejects ${name}`, () => assert.throws(() => cropPixels(value, width, 150)));
test("ambiguous element refuses capture before screenshot", async () => {
  let captures = 0;
  const api = { tabs: { get: async () => tab, captureVisibleTab: async () => { captures++; return ""; } }, scripting: { executeScript: async () => [{ frameId: 0, result: null }] } };
  await assert.rejects(visibleCrop(api, imageContext(context, tab), operationId, new AbortController().signal)); assert.equal(captures, 0);
});
test("moved target cannot reach screenshot, measurement or decode", async () => {
  let calls = 0, captures = 0;
  const api = { tabs: { get: async () => tab, captureVisibleTab: async () => { captures++; return "data:image/png;base64,"; } }, scripting: { executeScript: async () => { calls++; return [{ frameId: 0, result: { ...rect, x: calls } }]; } } };
  await assert.rejects(visibleCrop(api, imageContext(context, tab), operationId, new AbortController().signal), e => e.code === "ScreenshotOwnershipUnverified"); assert.equal(captures, 0); assert.equal(calls, 0);
});
for (const name of [...ownershipCases, "apparently-clear"]) test(`actual acquisition refuses unproven viewport ownership: ${name}`, async () => {
  const dom = installDom(name, context.srcUrl), oldFetch = globalThis.fetch;
  globalThis.fetch = async () => new Response(null, { status: 403 });
  try { await assert.rejects(acquireImage(imageContext(context, tab), new AbortController().signal), e => e.code === "ScreenshotOwnershipUnverified"); assert.equal(dom.hits, 0); }
  finally { globalThis.fetch = oldFetch; dom.restore(); }
});
function fakePort() {
  const port = { sent: [], disconnected: 0, onMessage: { addListener(f) { port.receive = f; } }, onDisconnect: { addListener(f) { port.lost = f; } }, postMessage(m) { port.sent.push(m); }, disconnect() { port.disconnected++; port.lost(); } };
  return port;
}
for (const scene of ["corner-overlay", "clip-path", "original"]) test(`actual service worker acquisition pipeline: ${scene}`, async () => {
  const oldChrome = Object.getOwnPropertyDescriptor(globalThis, "chrome"), oldFetch = globalThis.fetch;
  const dom = installDom(scene, context.srcUrl), port = fakePort(); let handler, captures = 0, scripts = 0, fetches = 0, title = "";
  const event = () => ({ addListener() {} });
  const api = {
    runtime: { onInstalled: event(), connectNative: () => port, openOptionsPage: async () => {} },
    contextMenus: { onClicked: { addListener(fn) { handler = fn; } }, create() {} },
    action: { onClicked: event(), setBadgeText: async () => {}, setTitle: async arg => { title = arg.title; } },
    windows: { onFocusChanged: event(), onRemoved: event(), getLastFocused: async () => ({ id: tab.windowId, focused: false }) },
    tabs: { onDetached: event(), onAttached: event(), onRemoved: event(), onActivated: event(), onUpdated: event(), get: async () => tab, captureVisibleTab: async () => { captures++; throw new Error("test viewport must never be read"); } },
    scripting: { executeScript: async () => { scripts++; throw new Error("test DOM must never be injected"); } }
  };
  port.postMessage = message => {
    port.sent.push(message);
    if (message.type === "CancelImage") return;
    const type = ({ OfferImage: "Ready", BeginImage: "Begun", Chunk: "ChunkAccepted", CommitImage: "Committed" })[message.type];
    queueMicrotask(() => port.receive({ type, operationId: message.operationId ?? message.transferId, protocol: 1, index: message.index }));
  };
  globalThis.fetch = async (url, opts) => { fetches++; assert.equal(url, context.srcUrl); assert.equal(opts.credentials, "omit"); assert.equal(opts.redirect, "error"); return scene === "original" ? new Response(png, { headers: { "content-type": "image/png" } }) : new Response(null, { status: 403 }); };
  Object.defineProperty(globalThis, "chrome", { value: api, configurable: true });
  try {
    await import(`../../extensions/chromium/service-worker.js?ownership=${scene}`);
    await handler(context, tab);
    assert.equal(fetches, 1); assert.equal(captures, 0); assert.equal(scripts, 0); assert.equal(dom.hits, 0);
    assert.deepEqual(port.sent.map(m => m.type), scene === "original" ? ["OfferImage", "BeginImage", "Chunk", "CommitImage"] : ["OfferImage", "CancelImage"]);
    if (scene === "original") { assert.equal(Buffer.from(port.sent.find(m => m.type === "Chunk").data, "base64").compare(png), 0); assert.match(title, /图片已交付本机/); }
    else assert.match(title, /截图暂不可用/);
  } finally { globalThis.fetch = oldFetch; if (oldChrome) Object.defineProperty(globalThis, "chrome", oldChrome); else delete globalThis.chrome; dom.restore(); }
});
test("disconnect rejects an outstanding frame immediately", async () => {
  const port = fakePort(), abort = new AbortController(), conn = nativeConnection(port, operationId, abort.signal, Date.now() + 10000);
  const pending = conn.exchange({ type: "OfferImage", operationId }, "Ready"); port.lost(); await assert.rejects(pending, /失联/); conn.close();
});
test("cancellation sends one explicit cancel and disconnects", async () => {
  const port = fakePort(), abort = new AbortController(), conn = nativeConnection(port, operationId, abort.signal, Date.now() + 10000);
  const pending = conn.exchange({ type: "OfferImage", operationId }, "Ready"); abort.abort(); await assert.rejects(pending, /取消/); assert.equal(port.sent[1].type, "CancelImage"); assert.equal(port.disconnected, 1); conn.close();
});
test("wrong operation cannot acknowledge pending frame", async () => {
  const port = fakePort(), conn = nativeConnection(port, operationId, new AbortController().signal, Date.now() + 10000);
  const pending = conn.exchange({ type: "OfferImage", operationId }, "Ready"); port.receive({ type: "Ready", operationId: "wrong", protocol: 1 }); await assert.rejects(pending, /身份/); conn.close();
});
test("desktop Off prevents image acquisition", async () => {
  const port = fakePort(), conn = nativeConnection(port, operationId, new AbortController().signal, Date.now() + 10000); let acquisitions = 0;
  const run = async () => { await conn.exchange({ type: "OfferImage", operationId }, "Ready"); acquisitions++; };
  const pending = run(); port.receive({ type: "Error", code: "ActivationRequired" }); await assert.rejects(pending, /ActivationRequired/); assert.equal(acquisitions, 0); conn.close();
});
// Test Chrome Port adapter over actual binary stdio. Real shared production host loop in a separately labelled test authority executable.
test("actual extension sender to independent host loop commits ordered maximum chunks", { timeout: 10000 }, async () => {
  const fixture = new URL("../../tools/KuaiYi.BrowserHostFixture/bin/Release/net10.0/KuaiYi.BrowserHostFixture.dll", import.meta.url);
  const process = spawn(processDotnet(), [fixture.pathname, "on"], { stdio: ["pipe", "pipe", "pipe"] });
  const exited = once(process, "close"); let stderr = "", buffered = Buffer.alloc(0);
  const port = fakePort();
  port.postMessage = m => { const bytes = Buffer.from(JSON.stringify(checkedFrame(m)), "utf8"), header = Buffer.alloc(4); header.writeUInt32LE(bytes.length); process.stdin.write(Buffer.concat([header, bytes])); };
  port.disconnect = () => { process.stdin.end(); };
  process.stderr.setEncoding("utf8"); process.stderr.on("data", s => { stderr += s; });
  process.stdout.on("data", chunk => {
    buffered = Buffer.concat([buffered, chunk]);
    while (buffered.length >= 4) {
      const length = buffered.readUInt32LE(); assert(length > 0 && length <= LIMIT); if (buffered.length < length + 4) break;
      const message = JSON.parse(buffered.subarray(4, length + 4).toString("utf8")); buffered = buffered.subarray(length + 4); port.receive(message);
    }
  });
  process.on("close", () => port.lost()); // Drain both stdio streams before reporting the simulated Chrome disconnect.
  const abort = new AbortController(), conn = nativeConnection(port, operationId, abort.signal, Date.now() + 8000);
  try {
    await conn.exchange({ type: "OfferImage", operationId }, "Ready");
    const bytes = new Uint8Array(CHUNK * 2 + 29); bytes.set(png); bytes.fill(255, png.length);
    await sendImage(conn.exchange, operationId, bytes, abort.signal); conn.close();
    const [code] = await exited; assert.equal(code, 0); assert.equal(buffered.length, 0);
    const counters = JSON.parse(stderr); assert.equal(counters.fixture, true); assert.equal(counters.realLocalPipe, true); assert.equal(counters.Submitted, 1); assert.equal(counters.Bytes, bytes.length); assert.equal(counters.Digest.toLowerCase(), Buffer.from(await crypto.subtle.digest("SHA-256", bytes)).toString("hex")); assert.equal(counters.Disposed, true);
  } finally { conn.close(); if (process.exitCode === null) process.kill(); }
});
function processDotnet() { return process.env.DOTNET_HOST_PATH || "dotnet"; }
