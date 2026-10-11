import { spawn } from "node:child_process";
import { once } from "node:events";
import { connect } from "node:net";
import { LIMIT, checkedFrame } from "../../extensions/chromium/protocol.js";
const realTimer = globalThis.setTimeout, realClear = globalThis.clearTimeout;

export async function receiverHarness() {
  const child = spawn(process.env.DOTNET_HOST_PATH || "dotnet", [new URL("../../tools/KuaiYi.BrowserHostFixture/bin/Release/net10.0/KuaiYi.BrowserHostFixture.dll", import.meta.url).pathname, "receiver-confirmation"], { stdio: ["pipe", "pipe", "pipe"] });
  const exit = once(child, "close"), events = [], eventWaiters = [], messages = [], messageWaiters = [];
  let buffered = Buffer.alloc(0), errorText = "", lineBuffer = "", control, commandId = 0, closed = false, holdReady = false, heldReady;
  const notify = (list, waiters, value) => { list.push(value); for (const w of [...waiters]) if (w.predicate(value)) { waiters.splice(waiters.indexOf(w), 1); w.resolve(value); } };
  const wait = (list, waiters, predicate) => list.some(predicate) ? Promise.resolve(list.find(predicate)) : new Promise(resolve => waiters.push({ predicate, resolve }));
  const port = {
    sent: [], disconnected: 0,
    onMessage: { addListener(fn) { port.receive = fn; } },
    onDisconnect: { addListener(fn) { port.lost = fn; } },
    postMessage(message) { port.sent.push(message); if (closed) throw new Error("closed test Port"); const data = Buffer.from(JSON.stringify(checkedFrame(message)), "utf8"), header = Buffer.alloc(4); header.writeUInt32LE(data.length); child.stdin.write(Buffer.concat([header, data])); },
    disconnect() { if (closed) return; closed = true; port.disconnected++; child.stdin.end(); port.lost?.(); }
  };
  child.stdin.on("error", error => { errorText += `${error.code}\n`; });
  child.stdout.on("data", data => {
    buffered = Buffer.concat([buffered, data]);
    while (buffered.length >= 4) {
      const length = buffered.readUInt32LE(); if (!length || length > LIMIT) throw new Error("bad actual native frame"); if (buffered.length < length + 4) break;
      const payload = buffered.subarray(4, length + 4), message = JSON.parse(payload.toString("utf8")); buffered = buffered.subarray(length + 4);
      if (!closed) { if (holdReady && message.type === "Ready") heldReady = message; else port.receive?.(message); }
      notify(messages, messageWaiters, message);
    }
  });
  child.stderr.setEncoding("utf8"); child.stderr.on("data", text => {
    lineBuffer += text;
    while (lineBuffer.includes("\n")) { const i = lineBuffer.indexOf("\n"), line = lineBuffer.slice(0, i); lineBuffer = lineBuffer.slice(i + 1); try { notify(events, eventWaiters, JSON.parse(line)); } catch { errorText += `${line}\n`; } }
  });
  child.on("close", () => { if (!closed) { closed = true; port.lost?.(); } });
  const ready = await wait(events, eventWaiters, e => e.eventType === "fixtureReady");
  control = connect(ready.address); await once(control, "connect"); control.on("error", e => { errorText += `${e.code}\n`; });
  return {
    port, messages, events, exit,
    waitEvent: type => wait(events, eventWaiters, e => e.eventType === type),
    waitMessage: type => wait(messages, messageWaiters, e => e.type === type),
    command(command, extra = {}) { const id = ++commandId; control.write(`${JSON.stringify({ command, id, ...extra })}\n`); return wait(events, eventWaiters, e => e.eventType === "controlAck" && e.id === id); },
    holdReady() { holdReady = true; },
    deliverHeldReady() { if (heldReady) port.receive?.(heldReady); },
    async close() {
      if (child.exitCode === null) { control.write(`${JSON.stringify({ command: "stop", id: ++commandId })}\n`); port.disconnect(); }
      control.end(); const timer = realTimer(() => { if (child.exitCode === null) child.kill(); }, 2000);
      try { await exit; if (buffered.length) throw new Error("partial test stdout"); } finally { realClear(timer); }
    },
    get errorText() { return errorText; }
  };
}
