using System;
using EHub.Application.Common.Interfaces.Services;

namespace EHub.Infrastructure.Services;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
