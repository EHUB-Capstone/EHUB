using System.Diagnostics;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Infrastructure.Options;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EHub.Infrastructure.Storage;

public sealed class LibreOfficeDocumentPreviewConverter : IDocumentPreviewConverter, IDisposable
{
    private const int MaximumSourceBytes = (int)SubmissionFileLimits.MaxPreviewConvertSizeBytes;
    private readonly DocumentPreviewOptions options;
    private readonly ILogger<LibreOfficeDocumentPreviewConverter> logger;
    private readonly SemaphoreSlim conversionSlots;

    public LibreOfficeDocumentPreviewConverter(
        IOptions<DocumentPreviewOptions> options,
        ILogger<LibreOfficeDocumentPreviewConverter> logger)
    {
        this.options = options.Value;
        this.logger = logger;
        conversionSlots = new SemaphoreSlim(
            this.options.MaximumConcurrentConversions,
            this.options.MaximumConcurrentConversions);
    }

    public async Task<Result<DocumentPreviewConversionResult>> ConvertToPdfAsync(
        byte[] sourceContent,
        string sourceExtension,
        CancellationToken cancellationToken = default)
    {
        var extension = sourceExtension.ToLowerInvariant();
        if (extension is not ".docx" and not ".pptx")
        {
            return Result.Failure<DocumentPreviewConversionResult>(
                ErrorCodes.WorkspaceFilePreviewUnsupported,
                "Only DOCX and PPTX files can be converted to PDF preview.");
        }

        if (sourceContent.Length is <= 0 or > MaximumSourceBytes)
        {
            return Result.Failure<DocumentPreviewConversionResult>(
                ErrorCodes.WorkspaceFilePreviewConversionFailed,
                "The source file is empty or exceeds the preview size limit.");
        }

        await conversionSlots.WaitAsync(cancellationToken);
        try
        {
            return await ConvertCoreAsync(sourceContent, extension, cancellationToken);
        }
        finally
        {
            conversionSlots.Release();
        }
    }

    private async Task<Result<DocumentPreviewConversionResult>> ConvertCoreAsync(
        byte[] sourceContent,
        string extension,
        CancellationToken cancellationToken)
    {
        var tempRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ehub-document-previews"));
        Directory.CreateDirectory(tempRoot);
        var workDirectory = Path.Combine(tempRoot, Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workDirectory, "output");
        var profileDirectory = Path.Combine(workDirectory, "profile");
        var sourceBaseName = "source";
        var sourcePath = Path.Combine(workDirectory, $"{sourceBaseName}{extension}");
        var outputPath = Path.Combine(outputDirectory, $"{sourceBaseName}.pdf");

        Directory.CreateDirectory(workDirectory);
        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(profileDirectory);

        try
        {
            await File.WriteAllBytesAsync(sourcePath, sourceContent, cancellationToken);
            var startInfo = new ProcessStartInfo
            {
                FileName = ResolveExecutablePath(),
                WorkingDirectory = workDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("--headless");
            startInfo.ArgumentList.Add("--nologo");
            startInfo.ArgumentList.Add("--nodefault");
            startInfo.ArgumentList.Add("--nolockcheck");
            startInfo.ArgumentList.Add("--nofirststartwizard");
            startInfo.ArgumentList.Add($"-env:UserInstallation={new Uri(profileDirectory + Path.DirectorySeparatorChar).AbsoluteUri}");
            startInfo.ArgumentList.Add("--convert-to");
            startInfo.ArgumentList.Add("pdf");
            startInfo.ArgumentList.Add("--outdir");
            startInfo.ArgumentList.Add(outputDirectory);
            startInfo.ArgumentList.Add(sourcePath);

            using var process = new Process { StartInfo = startInfo };
            try
            {
                if (!process.Start())
                {
                    return Unavailable("The document preview converter could not be started.");
                }
            }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                logger.LogError(exception, "LibreOffice could not be started for {Extension} preview conversion.", extension);
                return Unavailable("Document preview is temporarily unavailable because LibreOffice could not be started.");
            }

            var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(options.ConversionTimeoutSeconds));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                logger.LogWarning("LibreOffice preview conversion timed out for {Extension}.", extension);
                return Unavailable("Document preview conversion timed out. You can still download the original file.");
            }

            var outputLog = await standardOutput;
            var errorLog = await standardError;
            if (process.ExitCode != 0 || !File.Exists(outputPath))
            {
                logger.LogWarning(
                    "LibreOffice preview conversion failed for {Extension}. ExitCode: {ExitCode}; OutputLength: {OutputLength}; ErrorLength: {ErrorLength}.",
                    extension, process.ExitCode, outputLog.Length, errorLog.Length);
                return ConversionFailed();
            }

            var pdf = await File.ReadAllBytesAsync(outputPath, cancellationToken);
            if (pdf.Length < 5 || !pdf.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            {
                logger.LogWarning("LibreOffice produced an invalid PDF preview for {Extension}.", extension);
                return ConversionFailed();
            }

            return Result.Success(new DocumentPreviewConversionResult(pdf));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unexpected document preview conversion failure for {Extension}.", extension);
            return ConversionFailed();
        }
        finally
        {
            SafeDeleteWorkDirectory(tempRoot, workDirectory);
        }
    }

    private static Result<DocumentPreviewConversionResult> ConversionFailed() =>
        Result.Failure<DocumentPreviewConversionResult>(
            ErrorCodes.WorkspaceFilePreviewConversionFailed,
            "The document could not be converted for preview. You can still download the original file.");

    private static Result<DocumentPreviewConversionResult> Unavailable(string message) =>
        Result.Failure<DocumentPreviewConversionResult>(ErrorCodes.WorkspaceFilePreviewUnavailable, message);

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
    }

    private string ResolveExecutablePath()
    {
        if (!OperatingSystem.IsWindows() ||
            !string.Equals(options.LibreOfficeExecutablePath, "soffice", StringComparison.OrdinalIgnoreCase))
        {
            return options.LibreOfficeExecutablePath;
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var standardWindowsPath = Path.Combine(programFiles, "LibreOffice", "program", "soffice.exe");
        return File.Exists(standardWindowsPath) ? standardWindowsPath : options.LibreOfficeExecutablePath;
    }

    private static void SafeDeleteWorkDirectory(string tempRoot, string workDirectory)
    {
        try
        {
            var root = Path.GetFullPath(tempRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var target = Path.GetFullPath(workDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (target.StartsWith(root, StringComparison.OrdinalIgnoreCase) && Directory.Exists(workDirectory))
            {
                Directory.Delete(workDirectory, recursive: true);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void Dispose() => conversionSlots.Dispose();
}
