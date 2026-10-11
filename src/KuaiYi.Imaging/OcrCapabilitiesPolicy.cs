namespace KuaiYi.Imaging;
public static class OcrCapabilitiesPolicy
{
    public static void Require(WorkerDiagnostic diagnostic, string language, ImageSize image)
    {
        if (diagnostic.Failure != ImageFailure.None) throw new ImageJobException(diagnostic.Failure);
        if (!diagnostic.Identity.Present || diagnostic.Identity.Failure != ImageFailure.None) throw new ImageJobException(ImageFailure.IdentityMissing);
        if (diagnostic.Languages == null || diagnostic.MaxImageDimension == 0) throw new ImageJobException(ImageFailure.NativeFailure);
        if (!diagnostic.Languages.Contains(language, StringComparer.OrdinalIgnoreCase)) throw new ImageJobException(ImageFailure.LanguageUnavailable);
        ImagePolicy.ValidateDimensions(image.Width,image.Height);
        if (image.Width > diagnostic.MaxImageDimension || image.Height > diagnostic.MaxImageDimension) throw new ImageJobException(ImageFailure.OcrDimensionExceeded);
    }
}
