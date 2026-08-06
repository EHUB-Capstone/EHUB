using EHub.Shared.Results;
using EHub.Shared.Errors;
using Microsoft.AspNetCore.Http;

namespace EHub.Application.Features.Classes.Common;

internal enum ExcelWorkbookKind
{
    OpenXml,
    Binary,
    SpreadsheetMl
}

internal static class ExcelWorkbookSecurity
{
    private static readonly byte[] OleSignature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private static readonly HashSet<string> GenericMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        string.Empty,
        "application/octet-stream"
    };

    public static Result<ExcelWorkbookKind> Validate(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".xls", StringComparison.OrdinalIgnoreCase))
        {
            return Failure("Classes.InvalidFileType", "Only Excel files (.xlsx or .xls) are allowed.");
        }

        using var stream = file.OpenReadStream();
        Span<byte> signature = stackalloc byte[512];
        var bytesRead = stream.Read(signature);
        var prefix = signature[..bytesRead];

        ExcelWorkbookKind? kind = null;
        if (IsZip(prefix))
        {
            kind = ExcelWorkbookKind.OpenXml;
        }
        else if (prefix.Length >= OleSignature.Length && prefix[..OleSignature.Length].SequenceEqual(OleSignature))
        {
            kind = ExcelWorkbookKind.Binary;
        }
        else if (LooksLikeXml(prefix))
        {
            kind = ExcelWorkbookKind.SpreadsheetMl;
        }

        if (!kind.HasValue)
        {
            return Failure("Classes.InvalidFileSignature", "The file signature does not match a supported Excel workbook.");
        }

        var extensionMatches = kind.Value == ExcelWorkbookKind.OpenXml
            ? string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase)
            : string.Equals(extension, ".xls", StringComparison.OrdinalIgnoreCase);
        if (!extensionMatches)
        {
            return Failure("Classes.InvalidFileSignature", "The file extension does not match its Excel workbook signature.");
        }

        var mime = file.ContentType?.Trim() ?? string.Empty;
        var mimeMatches = GenericMimeTypes.Contains(mime) || kind.Value switch
        {
            ExcelWorkbookKind.OpenXml => string.Equals(
                mime,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                StringComparison.OrdinalIgnoreCase),
            ExcelWorkbookKind.Binary => string.Equals(mime, "application/vnd.ms-excel", StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(mime, "application/x-ole-storage", StringComparison.OrdinalIgnoreCase),
            ExcelWorkbookKind.SpreadsheetMl => string.Equals(mime, "application/vnd.ms-excel", StringComparison.OrdinalIgnoreCase) ||
                                             string.Equals(mime, "application/xml", StringComparison.OrdinalIgnoreCase) ||
                                             string.Equals(mime, "text/xml", StringComparison.OrdinalIgnoreCase),
            _ => false
        };

        return mimeMatches
            ? Result.Success(kind.Value)
            : Failure("Classes.InvalidFileMimeType", "The uploaded MIME type does not match the Excel workbook content.");
    }

    private static bool IsZip(ReadOnlySpan<byte> prefix) =>
        prefix.Length >= 4 && prefix[0] == 0x50 && prefix[1] == 0x4B &&
        ((prefix[2] == 0x03 && prefix[3] == 0x04) ||
         (prefix[2] == 0x05 && prefix[3] == 0x06) ||
         (prefix[2] == 0x07 && prefix[3] == 0x08));

    private static bool LooksLikeXml(ReadOnlySpan<byte> prefix)
    {
        if (prefix.Length >= 2 && prefix[0] == 0xFF && prefix[1] == 0xFE)
        {
            return LooksLikeUtf16Xml(prefix[2..], littleEndian: true);
        }

        if (prefix.Length >= 2 && prefix[0] == 0xFE && prefix[1] == 0xFF)
        {
            return LooksLikeUtf16Xml(prefix[2..], littleEndian: false);
        }

        var index = prefix.Length >= 3 && prefix[0] == 0xEF && prefix[1] == 0xBB && prefix[2] == 0xBF ? 3 : 0;
        while (index < prefix.Length && char.IsWhiteSpace((char)prefix[index]))
        {
            index++;
        }

        return index < prefix.Length && prefix[index] == (byte)'<';
    }

    private static bool LooksLikeUtf16Xml(ReadOnlySpan<byte> prefix, bool littleEndian)
    {
        for (var index = 0; index + 1 < prefix.Length; index += 2)
        {
            var character = littleEndian
                ? (char)(prefix[index] | (prefix[index + 1] << 8))
                : (char)((prefix[index] << 8) | prefix[index + 1]);
            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            return character == '<';
        }

        return false;
    }

    private static Result<ExcelWorkbookKind> Failure(string code, string message) =>
        Result.Failure<ExcelWorkbookKind>(new Error(code, message));
}
