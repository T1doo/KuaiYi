# Public OCR fixture

`kuaiyi-test.png` is a generated 640 × 160 RGB PNG, containing `KUAIYI TEST` in an explicit 5 × 7 bitmap glyph atlas scaled by eight. It contains no EXIF, GPS, screenshot or user content. It is suitable for a deterministic public OCR experiment; recognition is still **NotRun** on Windows. Recognition must match the normalized text exactly, not merely return nonempty output.

Historical P0 scope: its PNG decoder was not the full P6 SkiaSharp format/EXIF/alpha/resize pipeline; JPEG/WebP/BMP/GIF fixtures and production decoding were then still required. Subsequent bounded components and their limits are recorded under `docs/validation/`; this paragraph is not a current all-format completion claim.


## Synthetic GIF dictionary fixture (2026-10-20)

`gif-codewidth.gif` and `gif-codewidth.indices` are generated solely from synthetic indexed pixels, with the already installed Pillow 12.3.0. Pillow is a one-time fixture authoring tool, not a project/test runtime dependency. Python `random.Random(710020)` generates 32,768 `randrange(256)` indices, laid out row-major at 256×128. The 256-entry RGB palette maps index n to `(n, 255-n, n*37 % 256)`. `Image.frombytes('P', (256,128), indices)`, `putpalette(...)`, then `save(path, optimize=False, interlace=True)` produces the committed GIF. The `.indices` file stores the original uncompressed indices before encoding, an independent complete-pixel oracle. The test expands this palette itself; it does not use a decoder to generate expectations.

The actual GIF exercises 8-bit minimum code size, ordinary variable-width dictionary growth and interlaced storage. Explicit generated-code tests independently cover widths3–12, dictionary4096 saturation, reference4095, Clear reset, KwKwK and EOI across subblock boundaries. Other tiny GIF fixtures are built in test code from an explicit four-color palette and clear-per-pixel LZW. No private pictures, screenshots or copied third-party artwork are included.

- `gif-codewidth.gif`: 45666 bytes, SHA256 `3a328b251a019fab975dda90b27dc70fce45e28909131b410db0cd605295b7b2`.
- `gif-codewidth.indices`: 32768 bytes, SHA256 `e36de64fd7fc3c6981500cd98ca0b81ec32f6651c688e6000d2822012058f8ae`.

## Synthetic browser original rasters (2026-10-22)

`browser-raster.jpg`, `.bmp` and `.webp` contain a 3×2 RGB canvas, row-major grayscale values 10,50,90 / 130,180,230. They were authored with the already installed Pillow12.3.0, with no new project or test runtime dependency. Reproduction: `Image.new('RGB',(3,2))`, `putdata([(v,v,v) for v in [10,50,90,130,180,230]])`; JPEG uses `quality=100, subsampling=0, progressive=False`, BMP uses default24-bit BITMAPINFOHEADER, WebP uses `lossless=True, quality=100, method=6`. The WebP has one even22-byte VP8L payload; this is a fixture selection, never a relaxation of the product's odd-payload refusal.

`browser-raster-exif6.jpg` reopens that JPEG and saves it with the same JPEG options and `Image.Exif()[274]=6`. It has raw canvas3×2 and expected normalized canvas2×3. It contains only synthetic pixels and the numeric orientation, no GPS, device, filename or user data. Lossy JPEG pixel equality is not asserted; source digest, orientation, dimensions, actual Worker settlement and canonical PNG ownership are checked.

- `browser-raster-exif6.jpg`: 709 bytes, SHA256 `09c797b84676904cd50bc04c8cfe0fee9c2ae9b3b09b638b9e62a3447fca3c85`.
- `browser-raster.bmp`: 78 bytes, SHA256 `9cc375959f6d43a970b9bcf688bd6dbea586cf581c2151311039451a098c9948`.
- `browser-raster.jpg`: 674 bytes, SHA256 `103c11c20de1204f4e41c3959287a7a18de57f0cf9790325864f626935d8b367`.
- `browser-raster.webp`: 42 bytes, SHA256 `678b0e93646dfb2364607f2953b4e7b193a88f659b05449d22cea01bc2fd487d`.
