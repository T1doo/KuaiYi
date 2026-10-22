import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { createHash } from "node:crypto";
import { receiverHarness } from "./confirmation-harness.mjs";
const fixtures = [["kuaiyi-test.png", "image/png"], ["browser-raster.jpg", "image/jpeg"], ["browser-raster-exif6.jpg", "image/jpeg"], ["browser-raster.bmp", "image/bmp"], ["browser-raster.webp", "image/webp"], ["gif-codewidth.gif", "image/gif"], ["gif-codewidth.gif", "image/gif", 100], ["gif-codewidth.gif", "image/gif", 101], ["gif-codewidth.gif", "image/gif", 102]];
const bytesFor = name => readFile(new URL(`../fixtures/images/${name}`, import.meta.url));
const asData = (bytes, mime = "image/png") => `data:${mime};base64,${bytes.toString("base64")}`;
let serial = 0;
async function serviceWorker(h, srcUrl, settings = {}) {
  const oldChrome = Object.getOwnPropertyDescriptor(globalThis, "chrome"), oldFetch = globalThis.fetch, Native = globalThis.Uint8Array, arrays = [];
  globalThis.Uint8Array = new Proxy(Native, { construct(target, args) { const value = Reflect.construct(target, args, target); if (typeof args[0] === "number") arrays.push(value); return value; } });
  const listeners = {}, event = name => ({ addListener(fn) { listeners[name] = fn; } });
  let fetches = 0, queries = 0, title = "";
  const info = { menuItemId: "kuaiyi-image", mediaType: "image", pageUrl: "https://public-fixture.invalid/page", srcUrl, frameId: 0 };
  const tab = { id: 1, windowId: 2, url: info.pageUrl, active: true };
  const api = {
    runtime: { onInstalled: event("installed"), connectNative: () => h.port, openOptionsPage: async () => {} },
    contextMenus: { onClicked: event("click"), create() {} },
    action: { onClicked: event("stop"), setBadgeText: async () => {}, setTitle: async arg => { title = arg.title; } },
    tabs: { onRemoved: event("removed"), onActivated: event("activated"), onUpdated: event("updated"), onDetached: event("detached"), onAttached: event("attached"), get: async () => { queries++; return settings.readTab ? settings.readTab(queries, tab) : tab; } },
    windows: { onFocusChanged: event("focus"), onRemoved: event("windowRemoved"), getLastFocused: async () => ({ id: tab.windowId, focused: false }) }
  };
  globalThis.fetch = () => { fetches++; throw new Error("embedded acquisition must never fetch"); };
  Object.defineProperty(globalThis, "chrome", { value: api, configurable: true });
  await import(`../../extensions/chromium/service-worker.js?embedded=${++serial}`);
  return { arrays, start: () => listeners.click(info, tab), listeners, get fetches() { return fetches; }, get queries() { return queries; }, get title() { return title; }, restore() { globalThis.Uint8Array = Native; globalThis.fetch = oldFetch; if (oldChrome) Object.defineProperty(globalThis, "chrome", oldChrome); else delete globalThis.chrome; } };
}
for (const [file, mime, comment] of fixtures) test(`embedded actual ServiceWorker/stdIO/production receiver commits ${file}${comment ? ` across64KiB with${comment}subblocks` : ""}`, { timeout: 8000 }, async () => {
  let input = await bytesFor(file);
  if (comment) {
    // A valid GIF89a comment extension with bounded255-byte neutral subblocks before the original trailer.
    // This changes only encoded bytes; it crosses multiple decoder blocks without changing canvas pixels.
    assert.equal(input.at(-1), 0x3b); const block = Buffer.concat([Buffer.from([255]), Buffer.alloc(255, 75)]);
    input = Buffer.concat([input.subarray(0, -1), Buffer.from([0x21, 0xfe]), ...Array.from({ length: comment }, () => block), Buffer.from([0, 0x3b])]);
    input.write("GIF89a", 0, "ascii"); // The original synthetic fixture is87a, whose product subset refuses extensions.
    assert(input.length * 4 / 3 > 65536); assert.equal(input.length % 3, (comment - 99) % 3);
  }
  const h = await receiverHarness(); let worker, running;
  try {
    worker = await serviceWorker(h, asData(input, mime)); running = worker.start();
    await h.waitEvent("decisionAsked"); assert.deepEqual(h.port.sent.map(m => m.type), ["OfferImage"]); assert.equal(worker.fetches, 0); assert.equal(worker.queries, 0); assert.equal(worker.arrays.filter(b => b.length === input.length).length, 0);
    await h.command("allow"); await running;
    const final = await h.waitEvent("fixtureFinal"), [code] = await h.exit;
    assert.equal(code, 0); assert.equal(final.committed, true); assert.equal(final.bytes, input.length); assert.equal(final.digest.toLowerCase(), createHash("sha256").update(input).digest("hex"));
    assert.equal(final.realProductionReceiver, true); assert.equal(final.realLocalPipe, true); assert.equal(final.syntheticDecision, true); assert.equal(final.syntheticPeer, true); assert.equal(final.Calls, 1); assert.equal(final.retainedCleared, true); assert.equal(final.inboxEmpty, true); assert.equal(worker.fetches, 0); assert.equal(worker.queries, 2);
    assert.deepEqual(h.port.sent.map(m => m.type), ["OfferImage", "BeginImage", "Chunk", "CommitImage"]); assert.equal(h.port.sent[1].mime, mime); assert.equal(Buffer.from(h.port.sent[2].data, "base64").compare(input), 0); assert.match(worker.title, /图片已交付本机/); assert(!worker.title.includes("data:"));
    const owned = worker.arrays.filter(b => b.length === input.length); assert.equal(owned.length, 1); assert(owned[0].every(n => n === 0));
  } finally { h.port.disconnect(); if (running) await running; worker?.restore(); await h.close(); }
});
for (const scene of ["reject", "before-read-page-replaced", "after-read-page-replaced", "malformed", "mime-mismatch", "predicted-oversize", "cancel"]) test(`embedded actual receiver ${scene} has zero image sends and settles cleanup`, { timeout: 8000 }, async () => {
  const input = await bytesFor("kuaiyi-test.png"), h = await receiverHarness(); let worker, running;
  let raw = asData(input);
  if (scene === "malformed") raw += "#synthetic-private-marker";
  if (scene === "mime-mismatch") raw = asData(input, "image/gif");
  if (scene === "predicted-oversize") raw = "data:image/png;base64," + "A".repeat(4 * Math.ceil(20 * 1024 * 1024 / 3));
  try {
    worker = await serviceWorker(h, raw, { readTab: (query, tab) => scene === "before-read-page-replaced" || scene === "after-read-page-replaced" && query > 1 ? { ...tab, url: "https://public-fixture.invalid/replaced" } : tab });
    running = worker.start(); await h.waitEvent("decisionAsked");
    if (scene === "cancel") worker.listeners.stop();
    else await h.command(scene === "reject" ? "reject" : "allow");
    await running; const final = await h.waitEvent("fixtureFinal"); await h.exit;
    assert.equal(final.committed, false); assert.equal(final.bytes, 0); assert.equal(final.retainedCleared, true); assert.equal(final.inboxEmpty, true); assert.equal(worker.fetches, 0);
    assert(h.port.sent.every(m => ["OfferImage", "CancelImage"].includes(m.type))); assert(h.port.sent.filter(m => m.type === "CancelImage").length <= 1); assert(!worker.title.includes("data:")); assert(!worker.title.includes("synthetic-private-marker"));
    const owned = worker.arrays.filter(b => b.length === input.length); assert.equal(owned.length, ["after-read-page-replaced", "mime-mismatch"].includes(scene) ? 1 : 0); assert(owned.every(bytes => bytes.every(n => n === 0)));
  } finally { h.port.disconnect(); if (running) await running; worker?.restore(); await h.close(); }
});
