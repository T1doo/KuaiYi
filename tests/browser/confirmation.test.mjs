import test from "node:test";
import assert from "node:assert/strict";
import { setTimeout as delay } from "node:timers/promises";
import { readFile } from "node:fs/promises";
import { nativeConnection, sendImage, TIMING } from "../../extensions/chromium/protocol.js";
import { receiverHarness } from "./confirmation-harness.mjs";
const png = new Uint8Array(await readFile(new URL("../fixtures/images/kuaiyi-test.png", import.meta.url)));
test("actual extension/host/production receiver waits over five seconds for explicit synthetic desktop decision", { timeout: 12000 }, async () => {
  const h = await receiverHarness(), abort = new AbortController(), id = crypto.randomUUID();
  const conn = nativeConnection(h.port, id, abort.signal, Date.now() + 65000);
  const offering = conn.exchange({ type: "OfferImage", operationId: id }, "Ready"); let rejected = false; offering.catch(() => { rejected = true; });
  try {
    await h.waitEvent("decisionAsked"); await delay(6000); // Real elapsed extension budget, async wait only; no Windows UI claim.
    assert.equal(rejected, false, "valid desktop confirmation must remain pending beyond the old five-second offer timeout");
    assert.deepEqual(h.port.sent.map(m => m.type), ["OfferImage"]);
    await h.command("allow"); await offering; await sendImage(conn.exchange, id, png, abort.signal); conn.close();
    const final = await h.waitEvent("fixtureFinal"); assert.equal(final.committed, true); assert.equal(final.bytes, png.length); assert.equal(final.Calls, 1); assert.equal(final.retainedCleared, true); assert.equal(final.inboxEmpty, true);
    const [code] = await h.exit; assert.equal(code, 0);
  } finally { conn.close(); await h.close(); }
});
function clock(t) {
  t.mock.timers.enable({ apis: ["Date", "setTimeout"], now: 100000 });
  t.mock.method(performance, "now", () => Date.now());
}
async function actualWorker(h, scenario) {
  const oldChrome = Object.getOwnPropertyDescriptor(globalThis, "chrome"), oldFetch = globalThis.fetch;
  let clicked, cancel, acquireCount = 0, title = ""; const event = () => ({ addListener() {} });
  const info = { menuItemId: "kuaiyi-image", mediaType: "image", pageUrl: "https://public-fixture.invalid/page", srcUrl: "https://public-fixture.invalid/image.png", frameId: 0 };
  const tab = { id: 1, windowId: 2, url: info.pageUrl, active: true };
  const api = {
    runtime: { onInstalled: event(), connectNative: () => h.port, openOptionsPage: async () => {} },
    contextMenus: { onClicked: { addListener(fn) { clicked = fn; } }, create() {} },
    action: { onClicked: { addListener(fn) { cancel = fn; } }, setBadgeText: async () => {}, setTitle: async arg => { title = arg.title; } },
    windows: { onFocusChanged: event(), onRemoved: event(), getLastFocused: async () => ({ id: tab.windowId, focused: false }) },
    tabs: { onDetached: event(), onAttached: event(), onRemoved: event(), onActivated: event(), onUpdated: event(), get: async () => tab }
  };
  globalThis.fetch = async (url, opts) => { acquireCount++; assert.equal(url, info.srcUrl); assert.equal(opts.credentials, "omit"); assert.equal(opts.redirect, "error"); return new Response(png, { headers: { "content-type": "image/png" } }); };
  Object.defineProperty(globalThis, "chrome", { value: api, configurable: true });
  await import(`../../extensions/chromium/service-worker.js?confirmation=${scenario}`);
  const running = clicked(info, tab);
  return { running, cancel: () => cancel(), get acquisitions() { return acquireCount; }, get title() { return title; }, restore() { globalThis.fetch = oldFetch; if (oldChrome) Object.defineProperty(globalThis, "chrome", oldChrome); else delete globalThis.chrome; } };
}
for (const scenario of ["allow-after-six", "no-answer", "reject", "cancel", "off", "disconnect", "late-answer", "late-answer-no-timer", "late-ready"]) {
  test(`actual ServiceWorker/native host/production receiver confirmation boundary: ${scenario}`, { timeout: 10000 }, async t => {
    clock(t); const h = await receiverHarness(); let worker;
    try {
      if (scenario === "late-ready") h.holdReady();
      worker = await actualWorker(h, scenario);
      await h.waitMessage("ConfirmationPending"); await h.waitEvent("decisionAsked");
      assert.equal(worker.acquisitions, 0); assert.deepEqual(h.port.sent.map(m => m.type), ["OfferImage"]);
      if (scenario === "allow-after-six") {
        await h.command("advance", { milliseconds: 6000 }); t.mock.timers.tick(6000);
        assert.equal(worker.acquisitions, 0); assert.equal(h.port.disconnected, 0);
        await h.command("allow"); await worker.running;
      } else if (scenario === "no-answer" || scenario.startsWith("late-answer")) {
        if (scenario.startsWith("late-answer")) await h.command("advance", { milliseconds: TIMING.confirmation, lateAllow: true, noTimers: scenario === "late-answer-no-timer" });
        t.mock.timers.tick(TIMING.confirmation);
        await worker.running;
      } else if (scenario === "late-ready") {
        await h.command("allow"); await h.waitMessage("Ready");
        t.mock.timers.tick(TIMING.confirmation); h.deliverHeldReady(); await worker.running;
      } else {
        if (scenario === "cancel") worker.cancel();
        else await h.command(({ reject: "reject", off: "off", disconnect: "drop" })[scenario]);
        await worker.running;
      }
      const final = await h.waitEvent("fixtureFinal"), [code] = await h.exit;
      assert.equal(final.syntheticDecision, true); assert.equal(final.syntheticPeer, true); assert.equal(final.realProductionReceiver, true); assert.equal(final.realLocalPipe, true);
      assert.equal(final.retainedCleared, true); assert.equal(final.inboxEmpty, true); assert.equal(final.Calls, 1); assert.equal(h.port.disconnected, 1);
      if (scenario === "allow-after-six") {
        assert.equal(code, 0); assert.equal(final.committed, true); assert.equal(final.bytes, png.length); assert.equal(worker.acquisitions, 1); assert.match(worker.title, /图片已交付本机/);
        assert.deepEqual(h.port.sent.map(m => m.type), ["OfferImage", "BeginImage", "Chunk", "CommitImage"]);
      } else {
        assert.equal(code, scenario === "late-ready" ? 0 : 3); // Host had issued Ready in this case, so its valid CancelImage terminates normally.
        assert.equal(final.committed, false); assert.equal(final.bytes, 0); assert.equal(worker.acquisitions, 0);
        assert(h.port.sent.every(m => ["OfferImage", "CancelImage"].includes(m.type))); assert(h.port.sent.filter(m => m.type === "CancelImage").length <= 1);
        assert.equal(h.messages.filter(m => m.type === "ConfirmationPending").length, 1);
      }
    } finally { h.port.disconnect(); if (worker) { await worker.running; worker.restore(); } await h.close(); }
  });
}
function fakePort() {
  const port = { sent: [], disconnected: 0, onMessage: { addListener(fn) { port.receive = fn; } }, onDisconnect: { addListener(fn) { port.lost = fn; } }, postMessage(message) { port.sent.push(message); }, disconnect() { port.disconnected++; port.lost(); } };
  return port;
}
const waiting = id => ({ type: "ConfirmationPending", operationId: id, protocol: 1, timeoutMs: TIMING.confirmation });
test("offer without explicit confirmation state still expires at five seconds", t => {
  clock(t); const port = fakePort(), ctrl = new AbortController(), id = crypto.randomUUID(), conn = nativeConnection(port, id, ctrl.signal, Date.now() + TIMING.total);
  const offer = conn.exchange({ type: "OfferImage", operationId: id }, "Ready"), rejected = assert.rejects(offer, /超时/);
  t.mock.timers.tick(TIMING.handshake); conn.close(); assert.equal(port.disconnected, 1); assert.equal(port.sent.filter(m => m.type === "CancelImage").length, 1); return rejected;
});
test("ordinary Begin acknowledgement keeps its five-second budget after long confirmation", async t => {
  clock(t); const port = fakePort(), ctrl = new AbortController(), id = crypto.randomUUID(), conn = nativeConnection(port, id, ctrl.signal, Date.now() + TIMING.total);
  const offer = conn.exchange({ type: "OfferImage", operationId: id }, "Ready"); port.receive(waiting(id)); t.mock.timers.tick(6000);
  port.receive({ type: "Ready", operationId: id, protocol: 1 }); await offer;
  const begin = conn.exchange({ type: "BeginImage", transferId: id }, "Begun"), rejected = assert.rejects(begin, /超时/);
  t.mock.timers.tick(TIMING.handshake); await rejected; assert.equal(port.disconnected, 1); conn.close();
});
test("idle acquisition has a thirty-second transfer budget and disconnect cancels the owning signal", async t => {
  clock(t); const port = fakePort(), ctrl = new AbortController(), id = crypto.randomUUID(), conn = nativeConnection(port, id, ctrl.signal, Date.now() + TIMING.total, () => ctrl.abort());
  const offer = conn.exchange({ type: "OfferImage", operationId: id }, "Ready"); port.receive(waiting(id)); port.receive({ type: "Ready", operationId: id, protocol: 1 }); await offer;
  assert.equal(ctrl.signal.aborted, false); t.mock.timers.tick(TIMING.transfer); assert.equal(ctrl.signal.aborted, true); assert.equal(port.disconnected, 1);
  await assert.rejects(conn.exchange({ type: "BeginImage", transferId: id }, "Begun")); assert.equal(port.sent.filter(m => m.type === "CancelImage").length, 1); conn.close();
});
test("late Ready rejected by monotonic deadline even when wall clock moves backwards before timer dispatch", async t => {
  t.mock.timers.enable({ apis: ["Date", "setTimeout"], now: 100000 }); let monotonic = 0; t.mock.method(performance, "now", () => monotonic);
  const port = fakePort(), id = crypto.randomUUID(), conn = nativeConnection(port, id, new AbortController().signal, Date.now() + TIMING.total);
  const offer = conn.exchange({ type: "OfferImage", operationId: id }, "Ready"), rejected = assert.rejects(offer, /超时/); port.receive(waiting(id));
  t.mock.timers.setTime(1); monotonic = TIMING.confirmation; port.receive({ type: "Ready", operationId: id, protocol: 1 });
  await rejected; assert.equal(port.disconnected, 1); assert.equal(port.sent.filter(m => m.type === "BeginImage").length, 0); conn.close();
});
test("total operation deadline cannot be renewed by confirmation or Ready", async t => {
  clock(t); const port = fakePort(), id = crypto.randomUUID(), ctrl = new AbortController(), conn = nativeConnection(port, id, ctrl.signal, Date.now() + 35000, () => ctrl.abort());
  const offer = conn.exchange({ type: "OfferImage", operationId: id }, "Ready"); t.mock.timers.tick(4000); port.receive(waiting(id));
  t.mock.timers.tick(29999); port.receive({ type: "Ready", operationId: id, protocol: 1 }); await offer;
  t.mock.timers.tick(1001); assert.equal(ctrl.signal.aborted, true); assert.equal(port.disconnected, 1); conn.close();
});
for (const kind of ["id", "budget", "protocol", "extra", "duplicate"]) test(`confirmation stage cannot obtain a renewed timer with invalid progress: ${kind}`, async t => {
  clock(t); const port = fakePort(), id = crypto.randomUUID(), conn = nativeConnection(port, id, new AbortController().signal, Date.now() + TIMING.total);
  const offer = conn.exchange({ type: "OfferImage", operationId: id }, "Ready"), rejected = assert.rejects(offer, /确认阶段/); const progress = waiting(id);
  if (kind === "id") progress.operationId = crypto.randomUUID(); if (kind === "budget") progress.timeoutMs = TIMING.total; if (kind === "protocol") progress.protocol = 2; if (kind === "extra") progress.granted = true;
  if (kind === "duplicate") port.receive(waiting(id)); port.receive(progress);
  await rejected; assert.equal(port.disconnected, 1); port.receive({ type: "Ready", operationId: id, protocol: 1 }); assert(port.sent.every(m => ["OfferImage", "CancelImage"].includes(m.type))); conn.close();
});
