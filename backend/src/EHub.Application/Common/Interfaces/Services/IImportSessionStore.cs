using System;
using System.Collections.Generic;
using EHub.Contracts.Classes;

namespace EHub.Application.Common.Interfaces.Services;

public interface IImportSessionStore
{
    void SaveSession(Guid sessionId, Guid classId, Guid userId, List<ImportStudentRowPreviewDto> validRows);
    (Guid ClassId, Guid UserId, List<ImportStudentRowPreviewDto> ValidRows)? GetAndConsumeSession(Guid sessionId);
}
