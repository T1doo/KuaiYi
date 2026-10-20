import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { createServer } from "node:http";
import { once } from "node:events";
import { original } from "../../extensions/chromium/acquire.js";
import { IMAGE } from "../../extensions/chromium/protocol.js";

const png = new Uint8Array(await readFile(new URL("../fixtures/images/kuaiyi-test.png", import.meta.url)));
const context = { imageUrl: "https://public-fixture.invalid/image.png", origin: "https://public-fixture.invalid" };
const deferred = () => { let resolve; const promise = new Promise(r => { resolve = r; }); return { promise, resolve }; };
async function within(promise, milliseconds) {
  let timer;
  try { return await Promise.race([promise, new Promise((_, reject) => { timer = setTimeout(() => reject(new Error("stream operation did not settle")), milliseconds); })]); }
  finally { clearTimeout(timer); }
}
async function withFetch(fetch, run) {
  const old = globalThis.fetch;
  globalThis.fetch = fetch;
  try { await run(); } finally { globalThis.fetch = old; }
}
function source(headers, status = 200) {
  let cancelled = 0;
  const body = new ReadableStream({ cancel() { cancelled++; } });
  return { response: new Response(body, { status, headers }), body, get cancelled() { return cancelled; } };
}
for (const [name, headers, status] of [
  ["HTTP refusal", { "content-type": "image/png" }, 403],
  ["wrong MIME", { "content-type": "text/plain" }, 200],
  ["declared oversize", { "content-type": "image/png", "content-length": `${IMAGE + 1}` }, 200],
  ["invalid length", { "content-type": "image/png", "content-length": "invalid" }, 200]
]) test(`original cancels unread body on ${name}`, async () => {
  const s = source(headers, status);
  await withFetch(async () => s.response, async () => {
    await assert.rejects(original(context, new AbortController().signal));
    assert.equal(s.cancelled, 1);
    assert.equal(s.body.locked, false);
  });
});
test("original refuses an already cancelled operation before fetch", async () => {
  const abort = new AbortController(); abort.abort(); let calls = 0;
  await withFetch(async () => { calls++; return new Response(png); }, async () => {
    await assert.rejects(original(context, abort.signal));
    assert.equal(calls, 0);
  });
});
test("original clears the exact chunk that exceeds the stream byte cap", async () => {
  const bytes = new Uint8Array(IMAGE + 1).fill(73);
  const response = new Response(new ReadableStream({ start(c) { c.enqueue(bytes); c.close(); } }), { headers: { "content-type": "image/png" } });
  await withFetch(async () => response, async () => {
    await assert.rejects(original(context, new AbortController().signal), /过大/);
    assert(bytes.every(n => n === 0));
    assert.equal(response.body.locked, false);
  });
});
test("original rejects and clears a chunk delivered concurrently with cancellation", async () => {
  const abort = new AbortController(), bytes = png.slice();
  const response = new Response(new ReadableStream({ pull(c) { c.enqueue(bytes); c.close(); abort.abort(); } }, { highWaterMark: 0 }), { headers: { "content-type": "image/png" } });
  await withFetch(async () => response, async () => {
    await assert.rejects(original(context, abort.signal));
    assert(bytes.every(n => n === 0));
    assert.equal(response.body.locked, false);
  });
});
test("original cancellation settles a pending native stream read and awaits original cancel", { timeout: 2000 }, async () => {
  const abort = new AbortController(), reading = deferred(), cancelEntered = deferred(), cancelDone = deferred();
  let cancelled = 0, controller;
  const body = new ReadableStream({ start(c) { controller = c; }, pull() { reading.resolve(); }, cancel() { cancelled++; cancelEntered.resolve(); return cancelDone.promise; } }, { highWaterMark: 0 });
  await withFetch(async () => new Response(body, { headers: { "content-type": "image/png" } }), async () => {
    const operation = original(context, abort.signal); let settled = false;
    const outcome = operation.then(value => { settled = true; return { value }; }, error => { settled = true; return { error }; });
    await reading.promise; abort.abort();
    try {
      await within(cancelEntered.promise, 300);
      await new Promise(resolve => setImmediate(resolve));
      assert.equal(settled, false); assert.equal(body.locked, true);
    } finally { if (cancelled === 0) controller.close(); cancelDone.resolve(); await outcome; }
    assert.match((await outcome).error?.message ?? "", /取消/); assert.equal(cancelled, 1); assert.equal(body.locked, false);
  });
});
test("original retains header refusal and cancel failure together", async () => {
  const cleanup = new Error("synthetic cancellation failure");
  const body = new ReadableStream({ cancel() { throw cleanup; } });
  await withFetch(async () => new Response(body, { headers: { "content-type": "text/plain" } }), async () => {
    await assert.rejects(original(context, new AbortController().signal), error => error instanceof AggregateError && error.errors.some(e => /不是 PNG/.test(e.message)) && error.errors.includes(cleanup));
    assert.equal(body.locked, false);
  });
});
test("original successful native stream releases its lock and returns intact owned bytes", async () => {
  const bytes = png.slice();
  const response = new Response(new ReadableStream({ start(c) { c.enqueue(bytes); c.close(); } }), { headers: { "content-type": "image/png" } });
  await withFetch(async () => response, async () => {
    const result = await original(context, new AbortController().signal);
    assert.deepEqual(result, png); assert(bytes.every(n => n === 0));
    assert.equal(response.body.locked, false); result.fill(0);
  });
});
test("original loopback HTTP fetch returns the actual PNG without credentials or redirect", { timeout: 3000 }, async () => {
  const fetch = globalThis.fetch; let requests = 0;
  const server = createServer((req, res) => {
    requests++; assert.equal(req.headers.cookie, undefined); assert.equal(req.headers.authorization, undefined);
    res.writeHead(200, { "content-type": "image/png", "content-length": png.length }); res.end(png);
  });
  server.listen(0, "127.0.0.1"); await once(server, "listening");
  try {
    await withFetch(async (url, options) => {
      assert.equal(url, context.imageUrl); assert.equal(options.credentials, "omit"); assert.equal(options.redirect, "error");
      return fetch(`http://127.0.0.1:${server.address().port}/image.png`, options);
    }, async () => { const bytes = await original(context, new AbortController().signal); assert.deepEqual(bytes, png); bytes.fill(0); });
    assert.equal(requests, 1);
  } finally { server.closeAllConnections(); await new Promise(resolve => server.close(resolve)); }
});
test("actual service worker navigation cancels the image stream before any transfer frame", { timeout: 2000 }, async () => {
  const oldChrome = Object.getOwnPropertyDescriptor(globalThis, "chrome"), reading = deferred();
  const abortEvents = {}, sent = []; let clicked, updated, cancelled = 0;
  const event = () => ({ addListener() {} });
  const port = {
    onMessage: { addListener(fn) { abortEvents.receive = fn; } }, onDisconnect: { addListener() {} }, disconnect() {},
    postMessage(message) { sent.push(message); if (message.type === "OfferImage") queueMicrotask(() => abortEvents.receive({ type: "Ready", operationId: message.operationId, protocol: 1 })); }
  };
  const info = { menuItemId: "kuaiyi-image", mediaType: "image", pageUrl: "https://public-fixture.invalid/page", srcUrl: context.imageUrl, frameId: 0 };
  const tab = { id: 1, windowId: 2, url: info.pageUrl, active: true };
  const api = {
    runtime: { onInstalled: event(), connectNative: () => port },
    contextMenus: { onClicked: { addListener(fn) { clicked = fn; } } },
    action: { onClicked: event(), setBadgeText: async () => {}, setTitle: async () => {} },
    windows: { onFocusChanged: event(), onRemoved: event(), getLastFocused: async () => ({ id: tab.windowId, focused: false }) },
    tabs: { onDetached: event(), onAttached: event(), onRemoved: event(), onActivated: event(), onUpdated: { addListener(fn) { updated = fn; } }, get: async () => tab }
  };
  let controller;
  const body = new ReadableStream({ start(c) { controller = c; }, pull() { reading.resolve(); }, cancel() { cancelled++; } }, { highWaterMark: 0 });
  Object.defineProperty(globalThis, "chrome", { value: api, configurable: true });
  try {
    await withFetch(async () => new Response(body, { headers: { "content-type": "image/png" } }), async () => {
      await import(`../../extensions/chromium/service-worker.js?acquisition=navigation`);
      const running = clicked(info, tab);
      await reading.promise; updated(tab.id, { status: "loading" });
      try { await within(running, 300); }
      finally { if (cancelled === 0) controller.close(); await running; }
      assert.equal(cancelled, 1); assert.equal(body.locked, false);
      assert.deepEqual(sent.map(message => message.type), ["OfferImage", "CancelImage"]);
    });
  } finally { if (oldChrome) Object.defineProperty(globalThis, "chrome", oldChrome); else delete globalThis.chrome; }
});
