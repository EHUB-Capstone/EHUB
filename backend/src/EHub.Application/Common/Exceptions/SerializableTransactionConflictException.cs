using System;

namespace EHub.Application.Common.Exceptions;

public sealed class SerializableTransactionConflictException : Exception
{
    public SerializableTransactionConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
