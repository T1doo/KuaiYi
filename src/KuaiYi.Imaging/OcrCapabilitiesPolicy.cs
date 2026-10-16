namespace KuaiYi.Imaging;
public static class OcrCapabilitiesPolicy
{
    public const int LanguageLimit=256;
    public static void ValidateJob(OcrDiagnosticJob job)
    {if(job.SessionId==Guid.Empty||job.Version<=0||job.Epoch<=0)throw new InvalidDataException("Invalid OCR diagnostic job.");}
    public static void ValidateDiagnostic(WorkerDiagnostic diagnostic)
    {
        if(diagnostic.Identity is not {} identity||!Enum.IsDefined(diagnostic.Failure)||!Enum.IsDefined(identity.Failure)||
            diagnostic.Languages==null||diagnostic.Languages.Length>LanguageLimit||!BoundedIdentity(identity.FullName)||!BoundedIdentity(identity.ApplicationUserModelId))
            throw new InvalidDataException("Invalid OCR diagnostic.");
        var languages=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(string language in diagnostic.Languages)
            if(string.IsNullOrWhiteSpace(language)||language.Length>64||!char.IsAsciiLetter(language[0])||language.EndsWith('-')||language.Contains("--",StringComparison.Ordinal)||language.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-')||!languages.Add(language))throw new InvalidDataException("Invalid OCR languages.");
        if(diagnostic.Failure!=ImageFailure.None&&(diagnostic.Languages.Length!=0||diagnostic.MaxImageDimension!=0))throw new InvalidDataException("Failed OCR diagnostic carries capabilities.");
        if(diagnostic.Failure==ImageFailure.None&&(!identity.Windows||!identity.Present||identity.Failure!=ImageFailure.None||
            string.IsNullOrEmpty(identity.FullName)||string.IsNullOrEmpty(identity.ApplicationUserModelId)||!identity.FullName.StartsWith(PackageIdentity.DevelopmentName+"_",StringComparison.Ordinal)||
            !identity.ApplicationUserModelId.StartsWith(PackageIdentity.DevelopmentName+"_",StringComparison.Ordinal)||!identity.ApplicationUserModelId.EndsWith("!ImageWorker",StringComparison.Ordinal)||diagnostic.MaxImageDimension==0))throw new InvalidDataException("Invalid OCR capability success.");
    }
    private static bool BoundedIdentity(string? value)
    {
        if(value==null)return true;if(value.Length>4096)return false;
        for(int i=0;i<value.Length;i++){if(char.IsControl(value[i]))return false;if(char.IsHighSurrogate(value[i])){if(++i>=value.Length||!char.IsLowSurrogate(value[i]))return false;}else if(char.IsLowSurrogate(value[i]))return false;}
        return true;
    }
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
