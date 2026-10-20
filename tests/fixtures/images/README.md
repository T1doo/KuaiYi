# Public OCR fixture

`kuaiyi-test.png` is a generated 640 × 160 RGB PNG, containing `KUAIYI TEST` in an explicit 5 × 7 bitmap glyph atlas scaled by eight. It contains no EXIF, GPS, screenshot or user content. It is suitable for a deterministic public OCR experiment; recognition is still **NotRun** on Windows. Recognition must match the normalized text exactly, not merely return nonempty output.

Historical P0 scope: its PNG decoder was not the full P6 SkiaSharp format/EXIF/alpha/resize pipeline; JPEG/WebP/BMP/GIF fixtures and production decoding were then still required. Subsequent bounded components and their limits are recorded under `docs/validation/`; this paragraph is not a current all-format completion claim.


## Synthetic GIF dictionary fixture (2026-10-20)

`gif-codewidth.gif` and `gif-codewidth.indices` are generated solely from synthetic indexed pixels, with the already installed Pillow 12.3.0. Pillow is a one-time fixture authoring tool, not a project/test runtime dependency. Python `random.Random(710020)` generates 32,768 `randrange(256)` indices, laid out row-major at 256×128. The 256-entry RGB palette maps index n to `(n, 255-n, n*37 % 256)`. `Image.frombytes('P', (256,128), indices)`, `putpalette(...)`, then `save(path, optimize=False, interlace=True)` produces the committed GIF. The `.indices` file stores the original uncompressed indices before encoding, an independent complete-pixel oracle. The test expands this palette itself; it does not use a decoder to generate expectations.

The actual GIF exercises 8-bit minimum code size, ordinary variable-width dictionary growth and interlaced storage. Explicit generated-code tests independently cover widths3–12, dictionary4096 saturation, reference4095, Clear reset, KwKwK and EOI across subblock boundaries. Other tiny GIF fixtures are built in test code from an explicit four-color palette and clear-per-pixel LZW. No private pictures, screenshots or copied third-party artwork are included.

- `gif-codewidth.gif`: 45666 bytes, SHA256 `3a328b251a019fab975dda90b27dc70fce45e28909131b410db0cd605295b7b2`.
- `gif-codewidth.indices`: 32768 bytes, SHA256 `e36de64fd7fc3c6981500cd98ca0b81ec32f6651c688e6000d2822012058f8ae`.
