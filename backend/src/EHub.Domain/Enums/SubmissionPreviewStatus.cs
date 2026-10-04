namespace EHub.Domain.Enums;

/// <summary>State of the PDF preview that is generated in the background for DOCX/PPTX submission files.</summary>
public enum SubmissionPreviewStatus
{
    /// <summary>No background generation requested (PDF files, files too large to convert, or not queued yet).</summary>
    None = 0,
    Pending = 1,
    Ready = 2,
    Failed = 3
}
