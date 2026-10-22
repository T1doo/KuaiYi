import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { original, imageContext } from "../../extensions/chromium/acquire.js";
import { IMAGE } from "../../extensions/chromium/protocol.js";

const fixtures = [["kuaiyi-test.png", "image/png"], ["browser-raster.jpg", "image/jpeg"], ["browser-raster-exif6.jpg", "image/jpeg"], ["browser-raster.bmp", "image/bmp"], ["browser-raster.webp", "image/webp"], ["gif-codewidth.gif", "image/gif"]];
const bytesFor = name => readFile(new URL(`../fixtures/images/${name}`, import.meta.url));
const dataUrl = (bytes, mime = "image/png") => `data:${mime};base64,${Buffer.from(bytes).toString("base64")}`;
const context = imageUrl => ({ imageUrl, origin: "https://public-fixture.invalid" });
async function noFetch(run) {
  const saved = globalThis.fetch; let calls = 0;
  globalThis.fetch = () => { calls++; throw new Error("data original must never fetch"); };
  try { await run(); assert.equal(calls, 0); } finally { globalThis.fetch = saved; }
}
for (const [file, mime] of fixtures) test(`embedded original accepts actual ${mime} ${file} bytes without fetch`, async () => {
  const input = new Uint8Array(await bytesFor(file));
  await noFetch(async () => { const bytes = await original(context(dataUrl(input, mime)), new AbortController().signal); assert.deepEqual(bytes, input); bytes.fill(0); });
});

const realSetTimeout = globalThis.setTimeout;
const immediate = () => new Promise(resolve => setImmediate(resolve));
async function observeArrays(run) {
  const Native = globalThis.Uint8Array, allocations = [];
  globalThis.Uint8Array = new Proxy(Native, { construct(target, args) { const value = Reflect.construct(target, args, target); if (typeof args[0] === "number") allocations.push(value); return value; } });
  try { await run(allocations); } finally { globalThis.Uint8Array = Native; }
}
const rejected = error => error.code === "InvalidDataImage" && error.message === "内嵌图片的类型、编码或大小无效";
for (const [index, raw] of [
  "data:image/svg+xml;base64,PHN2Zy8+", "data:image/png,AAAA", "data:image/png;charset=utf-8;base64,AAAA", "data:image/png;BASE64,AAAA",
  "data:image/png;base64,", "data:image/png;base64,A", "data:image/png;base64,AAA", "data:image/png;base64,AAAA=", "data:image/png;base64,====",
  "data:image/png;base64,A===", "data:image/png;base64,AA=A", "data:image/png;base64,AAA\n", "data:image/png;base64,AAA ",
  "data:image/png;base64,AAA_", "data:image/png;base64,AAA-", "data:image/png;base64,AAA%2B", "data:image/png;base64,AAAA#synthetic-private-marker",
  "data:image/png;base64,AB==", "data:image/png;base64,AAB="
].entries()) test(`embedded rejects malformed declaration/encoding case ${index}`, async () => {
  await noFetch(() => observeArrays(async arrays => { await assert.rejects(original(context(raw), new AbortController().signal), rejected); assert.equal(arrays.length, 0); }));
});
for (const mime of ["image/jpeg", "image/bmp", "image/webp", "image/gif"]) test(`embedded MIME ${mime} cannot relabel PNG and clears its exact allocation`, async () => {
  const png = await bytesFor("kuaiyi-test.png"), raw = dataUrl(png, mime);
  await noFetch(() => observeArrays(async arrays => {
    await assert.rejects(original(context(raw), new AbortController().signal), rejected);
    const owned = arrays.filter(bytes => bytes.length === png.length); assert.equal(owned.length, 1); assert(owned[0].every(n => n === 0));
  }));
});
test("embedded length and predicted decoded cap both reject before allocation or scheduled work", async () => {
  const maximumEncoded = 4 * Math.ceil(IMAGE / 3);
  // Exactly the maximum encoded length but no padding predicts20MiB+1, which must be refused too.
  for (const body of ["A".repeat(maximumEncoded + 4), "A".repeat(maximumEncoded)]) await noFetch(() => observeArrays(async arrays => {
    let scheduled = 0; const saved = globalThis.setTimeout;
    globalThis.setTimeout = (...args) => { scheduled++; return saved(...args); };
    try { await assert.rejects(original(context(`data:image/png;base64,${body}`), new AbortController().signal), rejected); assert.equal(arrays.length, 0); assert.equal(scheduled, 0); }
    finally { globalThis.setTimeout = saved; }
  }));
});
test("embedded data stays literal and is never passed to URL or fetch", async () => {
  const png = await bytesFor("kuaiyi-test.png"), raw = dataUrl(png), saved = globalThis.URL;
  const info = { menuItemId: "kuaiyi-image", mediaType: "image", pageUrl: "https://public-fixture.invalid/page", srcUrl: raw + "#synthetic-private-marker", frameId: 0 };
  const tab = { id: 1, windowId: 2, active: true, url: info.pageUrl };
  globalThis.URL = new Proxy(saved, { construct(target, args) { assert(!String(args[0]).startsWith("data:")); return Reflect.construct(target, args); } });
  try {
    const selected = imageContext(info, tab); assert.equal(selected.imageUrl, info.srcUrl);
    await noFetch(async () => { await assert.rejects(original(selected, new AbortController().signal), rejected); const result = await original(context(raw), new AbortController().signal); assert.deepEqual(Buffer.from(result), png); result.fill(0); });
    for (const srcUrl of [undefined, {}, "data:image/png;base64," + "A".repeat(4 * Math.ceil(IMAGE / 3) + 4)]) assert.throws(() => imageContext({ ...info, srcUrl }, tab), rejected);
  } finally { globalThis.URL = saved; }
});
test("already cancelled embedded operation never allocates or schedules a decode", async () => {
  const png = await bytesFor("kuaiyi-test.png"), controller = new AbortController(); controller.abort();
  await noFetch(() => observeArrays(async arrays => { await assert.rejects(original(context(dataUrl(png)), controller.signal), e => e.code === "Cancelled"); assert.equal(arrays.length, 0); }));
});
for (const phase of ["validation", "decoding"]) test(`embedded ${phase} cancellation joins the held original task and clears its sole output`, { timeout: 3000 }, async () => {
  const png = await bytesFor("kuaiyi-test.png"), source = new Uint8Array(150000).fill(73); source.set(png);
  // Synthetic byte stress buffer, not a claim that its trailing bytes are a valid PNG container for Worker.
  const raw = dataUrl(source), controller = new AbortController(), queued = [];
  const saved = globalThis.setTimeout; let running, outcome, settled = false;
  globalThis.setTimeout = (callback, delay, ...args) => delay === 0 ? (queued.push(() => callback(...args)), queued.length) : realSetTimeout(callback, delay, ...args);
  try {
    await noFetch(() => observeArrays(async arrays => {
      running = original(context(raw), controller.signal); outcome = running.then(value => { settled = true; return { value }; }, error => { settled = true; return { error }; });
      for (let attempt = 0; attempt < 30; attempt++) {
        await immediate();
        const owned = arrays.find(b => b.length === source.length);
        if (queued.length && (phase === "validation" || owned)) break;
        queued.shift()?.();
      }
      const owned = arrays.filter(b => b.length === source.length); assert.equal(owned.length, phase === "decoding" ? 1 : 0); assert(queued.length > 0); assert.equal(settled, false);
      if (phase === "decoding") assert(owned[0].some(n => n !== 0));
      controller.abort(); if (owned.length) assert(owned[0].every(n => n === 0)); assert.equal(settled, false);
      while (queued.length) queued.shift()();
      const result = await outcome; assert.equal(result.error?.code, "Cancelled"); assert.equal(result.value, undefined); if (owned.length) assert(owned[0].every(n => n === 0));
    }));
  } finally {
    controller.abort(); globalThis.setTimeout = saved; while (queued.length) queued.shift()(); if (outcome) await outcome;
  }
});
