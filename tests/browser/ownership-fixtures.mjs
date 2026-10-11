// Test-only DOM/API fixture data. Tests execute actual production exports, not a replacement measurement algorithm.
export const ownershipCases = ["corner-overlay", "edge-overlay", "opposite-corner-overlay", "clip-path", "border", "padding", "object-fit", "mask"];
export function installDom(name, imageUrl) {
  const keys = ["document", "getComputedStyle", "visualViewport", "innerWidth", "innerHeight", "devicePixelRatio", "scrollX", "scrollY", "location"];
  const previous = new Map(keys.map(key => [key, Object.getOwnPropertyDescriptor(globalThis, key)]));
  const overlay = { fixture: "other content" }, background = { fixture: "page background" };
  const element = { currentSrc: imageUrl, src: imageUrl, complete: true, naturalWidth: name === "object-fit" ? 80 : 100, naturalHeight: name === "object-fit" ? 40 : 100, parentElement: null,
    getBoundingClientRect: () => ({ left: 0, top: 0, right: 100, bottom: 100, width: 100, height: 100 }) };
  let hits = 0;
  const style = { visibility: "visible", display: "block", opacity: "1", transform: "none", overflowX: "visible", overflowY: "visible", clipPath: name === "clip-path" ? "circle(40%)" : "none", borderTopWidth: name === "border" ? "10px" : "0px", paddingTop: name === "padding" ? "10px" : "0px", objectFit: name === "object-fit" ? "contain" : "fill", maskImage: name === "mask" ? "linear-gradient(black, transparent)" : "none" };
  const values = {
    document: { images: [element], elementFromPoint(x, y) { hits++; if (name === "corner-overlay" && x < 10 && y < 10 || name === "edge-overlay" && x < 10 && y > 35 && y < 65 || name === "opposite-corner-overlay" && x > 90 && y > 90) return overlay; if ((name === "clip-path" || name === "mask") && x < 10 && y < 10) return background; return element; } },
    getComputedStyle: () => style, visualViewport: { scale: 1, offsetLeft: 0, offsetTop: 0 }, innerWidth: 100, innerHeight: 100, devicePixelRatio: 1, scrollX: 0, scrollY: 0, location: { href: "https://public-fixture.invalid/page" }
  };
  for (const [key, value] of Object.entries(values)) Object.defineProperty(globalThis, key, { value, configurable: true, writable: true });
  return { get hits() { return hits; }, restore() { for (const key of keys) { const old = previous.get(key); if (old) Object.defineProperty(globalThis, key, old); else delete globalThis[key]; } } };
}
