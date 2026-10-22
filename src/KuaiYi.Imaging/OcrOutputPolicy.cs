namespace KuaiYi.Imaging;

public static class OcrOutputPolicy
{
    public static void Validate(OcrOutput result, ImageSize expected, string language)
    {
        if (result.Width != expected.Width || result.Height != expected.Height || !string.Equals(result.Language, language, StringComparison.OrdinalIgnoreCase) ||
            result.Text == null || result.Text.Length > 50_000 || result.Lines == null || result.Lines.Length > 10_000) throw new InvalidDataException("Invalid OCR output.");
        int count = 0;
        foreach (var line in result.Lines)
        {
            if (line == null || line.Text == null || line.Text.Length > 50_000 || line.Words == null || line.Words.Length > 50_000 - count) throw new InvalidDataException("Invalid OCR line.");
            count += line.Words.Length;
            foreach (var word in line.Words)
                if (word == null || word.Text == null || word.Text.Length > 50_000 || !double.IsFinite(word.X) || !double.IsFinite(word.Y) || !double.IsFinite(word.Width) || !double.IsFinite(word.Height) ||
                    word.X < 0 || word.Y < 0 || word.Width < 0 || word.Height < 0 || word.X > expected.Width || word.Y > expected.Height ||
                    word.Width > expected.Width-word.X || word.Height > expected.Height-word.Y) throw new InvalidDataException("Invalid OCR word bounds.");
        }
    }
}
