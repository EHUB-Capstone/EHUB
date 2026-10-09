using System;

namespace EHub.Application.Common.Interfaces.Services;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
