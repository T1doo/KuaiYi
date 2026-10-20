import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { imageContext } from "../../extensions/chromium/acquire.js";

const png = new Uint8Array(await readFile(new URL("../fixtures/images/kuaiyi-test.png", import.meta.url)));
const info = { menuItemId: "kuaiyi-image", mediaType: "image", pageUrl: "https://public-fixture.invalid/page", srcUrl: "https://public-fixture.invalid/image.png", frameId: 0 };
const tab = { id: 1, windowId: 2, url: info.pageUrl, active: true };
const deferred = () => { let resolve; const promise = new Promise(r => { resolve = r; }); return { promise, resolve }; };
let serial = 0;
async function worker(run, configure = () => {}) {
  const oldChrome = Object.getOwnPropertyDescriptor(globalThis, "chrome"), oldFetch = globalThis.fetch;
  const listeners = {}, event = name => ({ addListener(fn) { listeners[name] = fn; } });
  const sent = []; let disconnected = 0, fetches = 0, queries = 0, title = "", readTab = async () => ({ ...tab }), readWindow = async () => ({ id: tab.windowId, focused: true });
  const port = {
    onMessage: event("message"), onDisconnect: event("lost"),
    disconnect() { disconnected++; listeners.lost?.(); },
    postMessage(message) {
      sent.push(message);
      if (message.type === "CancelImage") return;
      const type = ({ OfferImage: "Ready", BeginImage: "Begun", Chunk: "ChunkAccepted", CommitImage: "Committed" })[message.type];
      queueMicrotask(() => listeners.message({ type, operationId: message.operationId ?? message.transferId, protocol: 1, index: message.index }));
    }
  };
  const api = {
    runtime: { onInstalled: event("installed"), connectNative: () => port, openOptionsPage: async () => {} },
    contextMenus: { onClicked: event("click"), create() {} },
    action: { onClicked: event("stop"), setBadgeText: async () => {}, setTitle: async arg => { title = arg.title; } },
    tabs: { onRemoved: event("tabRemoved"), onActivated: event("activated"), onUpdated: event("updated"), onDetached: event("detached"), onAttached: event("attached"), get: id => { assert.equal(id, tab.id); return readTab(); } },
    windows: { onFocusChanged: event("focus"), onRemoved: event("windowRemoved"), getLastFocused: () => { queries++; return readWindow(); } }
  };
  const state = {
    api, port, sent, listeners,
    get fetches() { return fetches; }, get queries() { return queries; }, get disconnected() { return disconnected; }, get title() { return title; },
    set readTab(fn) { readTab = fn; }, set readWindow(fn) { readWindow = fn; },
    fetch: async () => new Response(png.slice(), { headers: { "content-type": "image/png" } }),
    start: () => listeners.click(info, { ...tab })
  };
  configure(state);
  globalThis.fetch = (...args) => { fetches++; return state.fetch(...args); };
  Object.defineProperty(globalThis, "chrome", { value: api, configurable: true });
  try { await import(`../../extensions/chromium/service-worker.js?sourceWindow=${++serial}`); await run(state); }
  finally { globalThis.fetch = oldFetch; if (oldChrome) Object.defineProperty(globalThis, "chrome", oldChrome); else delete globalThis.chrome; }
}
const noTransfer = state => {
  assert.deepEqual(state.sent.map(m => m.type), ["OfferImage", "CancelImage"]);
  assert.equal(state.disconnected, 1);
};
test("source context rejects unavailable or synthetic tab/window identifiers", () => {
  for (const field of ["id", "windowId"]) for (const value of [undefined, -1, -2, 1.5, "2"])
    assert.throws(() => imageContext(info, { ...tab, [field]: value }), `${field}=${value}`);
});
for (const scene of ["different-window", "moved-tab", "wrong-tab", "inactive", "navigated", "tab-query-failed", "window-query-failed"]) {
  test(`source snapshot before image read rejects ${scene}`, { timeout: 2000 }, () => worker(async s => {
    if (scene === "different-window") s.readWindow = async () => ({ id: 3, focused: true });
    if (scene === "moved-tab") s.readTab = async () => ({ ...tab, windowId: 3 });
    if (scene === "wrong-tab") s.readTab = async () => ({ ...tab, id: 9 });
    if (scene === "inactive") s.readTab = async () => ({ ...tab, active: false });
    if (scene === "navigated") s.readTab = async () => ({ ...tab, url: "https://public-fixture.invalid/other" });
    if (scene === "tab-query-failed") s.readTab = async () => { throw new Error("synthetic closed tab"); };
    if (scene === "window-query-failed") s.readWindow = async () => { throw new Error("synthetic closed window"); };
    await s.start(); assert.equal(s.fetches, 0); noTransfer(s);
  }));
}
test("source snapshot after reading refuses a different last browser window even without a focus event", { timeout: 2000 }, () => worker(async s => {
  s.readWindow = async () => ({ id: s.fetches ? 3 : 2, focused: true });
  await s.start(); assert.equal(s.fetches, 1); noTransfer(s);
}));
for (const event of ["focus", "detached", "attached", "windowRemoved"]) {
  test(`source ${event} cancels a real pending stream and clears owned chunks`, { timeout: 2000 }, () => worker(async s => {
    const reading = deferred(), owned = png.slice(); let pulls = 0, cancelled = 0, controller;
    const body = new ReadableStream({ start(c) { controller = c; }, pull(c) { if (++pulls === 1) c.enqueue(owned); else reading.resolve(); }, cancel() { cancelled++; } }, { highWaterMark: 0 });
    s.fetch = async () => new Response(body, { headers: { "content-type": "image/png" } });
    const running = s.start();
    try {
      await reading.promise;
      if (event === "focus") { s.listeners.focus?.(3); s.listeners.focus?.(2); }
      else if (event === "windowRemoved") s.listeners.windowRemoved?.(2);
      else s.listeners[event]?.(tab.id, {});
      // Baseline lacks these events; close its stream so a failure never leaves work running.
      await new Promise(resolve => setImmediate(resolve)); if (!cancelled) controller.close();
      await running;
      assert.equal(cancelled, 1); assert.equal(body.locked, false); assert(owned.every(n => n === 0)); noTransfer(s);
    } finally { if (!cancelled) { try { controller.close(); } catch {} } await running; }
  }));
}
test("different-window focus during Chunk acknowledgement cancels without Commit", { timeout: 2000 }, () => worker(async s => {
  const originalPost = s.port.postMessage;
  s.port.postMessage = message => {
    if (message.type === "Chunk") { s.sent.push(message); s.listeners.focus?.(3); queueMicrotask(() => s.listeners.message({ type: "ChunkAccepted", operationId: message.transferId, index: message.index })); }
    else originalPost(message);
  };
  await s.start(); assert.equal(s.fetches, 1); assert.equal(s.sent.filter(m => m.type === "Chunk").length, 1);
  assert.equal(s.sent.filter(m => m.type === "CommitImage").length, 0); assert.equal(s.sent.filter(m => m.type === "CancelImage").length, 1); assert.equal(s.disconnected, 1);
}));
test("focus change during source query cannot be undone by a stale successful result", { timeout: 2000 }, () => worker(async s => {
  const entered = deferred(), result = deferred();
  s.readWindow = () => { entered.resolve(); return result.promise; };
  const running = s.start();
  try { await Promise.race([entered.promise, running]); s.listeners.focus?.(3); s.listeners.focus?.(2); }
  finally { result.resolve({ id: 2, focused: true }); await running; }
  assert.equal(s.fetches, 0); noTransfer(s);
}));
test("desktop focus loss and same browser window retain explicit confirmation and intact bytes", { timeout: 2000 }, () => worker(async s => {
  const originalPost = s.port.postMessage;
  s.port.postMessage = message => {
    if (message.type === "OfferImage") {
      s.sent.push(message);
      queueMicrotask(() => {
        s.listeners.message({ type: "ConfirmationPending", operationId: message.operationId, protocol: 1, timeoutMs: 30000 });
        s.listeners.focus?.(-1); // Native desktop confirmation is allowed to take focus.
        s.listeners.focus?.(2);
        s.listeners.detached?.(99, {}); s.listeners.attached?.(99, {}); s.listeners.windowRemoved?.(99);
        s.listeners.message({ type: "Ready", operationId: message.operationId, protocol: 1 });
      });
    } else originalPost(message);
  };
  s.readWindow = async () => ({ id: 2, focused: false });
  await s.start();
  assert.equal(s.fetches, 1); assert.equal(s.disconnected, 1);
  assert.deepEqual(s.sent.map(m => m.type), ["OfferImage", "BeginImage", "Chunk", "CommitImage"]);
  assert.deepEqual(new Uint8Array(Buffer.from(s.sent.find(m => m.type === "Chunk").data, "base64")), png);
  assert.match(s.title, /图片已交付本机/);
}));
