using System;

namespace EHub.Application.Common.Exceptions;

public abstract class AppException : Exception
{
    protected AppException(string message, string code)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
