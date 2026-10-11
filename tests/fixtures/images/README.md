# Public OCR fixture

`kuaiyi-test.png` is a generated 640 × 160 RGB PNG, containing `KUAIYI TEST` in an explicit 5 × 7 bitmap glyph atlas scaled by eight. It contains no EXIF, GPS, screenshot or user content. It is suitable for a deterministic public OCR experiment; recognition is still **NotRun** on Windows. Recognition must match the normalized text exactly, not merely return nonempty output.

P0's PNG decoder is not the full P6 SkiaSharp format/EXIF/alpha/resize pipeline. JPEG/WebP/BMP/GIF fixtures and full production decoding remain required in P6.
