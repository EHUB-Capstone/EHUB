namespace EHub.Domain.Enums;

/// <summary>Where the bytes of a <c>SubmissionFile</c> live. Rows created before R2 default to Cloudinary.</summary>
public enum SubmissionStorageProvider
{
    Cloudinary = 0,
    R2 = 1
}
