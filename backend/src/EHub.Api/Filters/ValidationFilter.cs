using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using EHub.Contracts.Common;

namespace EHub.Api.Filters;

public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;

            var argumentType = argument.GetType();
            
            // Build generic type IValidator<T>
            var validatorType = typeof(IValidator<>).MakeGenericType(argumentType);
            
            // Resolve from container
            var validator = context.HttpContext.RequestServices.GetService(validatorType) as IValidator;

            if (validator is not null)
            {
                var validationContextType = typeof(ValidationContext<>).MakeGenericType(argumentType);
                var validationContext = Activator.CreateInstance(validationContextType, argument) as IValidationContext;

                if (validationContext is not null)
                {
                    var validationResult = await validator.ValidateAsync(validationContext);

                    if (!validationResult.IsValid)
                    {
                        var errors = validationResult.Errors
                            .Select(failure => new ValidationError
                            {
                                Field = ConvertToCamelCase(failure.PropertyName),
                                Message = failure.ErrorMessage,
                                Code = failure.ErrorCode
                            });

                        throw new EHub.Application.Common.Exceptions.ValidationException(errors);
                    }
                }
            }
        }

        await next();
    }

    private static string ConvertToCamelCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        var parts = s.Split('.');
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (!string.IsNullOrEmpty(part) && char.IsUpper(part[0]))
            {
                parts[i] = char.ToLowerInvariant(part[0]) + part.Substring(1);
            }
        }
        return string.Join(".", parts);
    }
}
