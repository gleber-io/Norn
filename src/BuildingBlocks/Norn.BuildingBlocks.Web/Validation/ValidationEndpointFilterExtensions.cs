using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Norn.BuildingBlocks.Web.Validation;

/// <summary>Validação com FluentValidation aplicada por endpoint filter, nunca dentro do handler (§7.1).</summary>
public static class ValidationEndpointFilterExtensions
{
    public static RouteHandlerBuilder WithValidation<TRequest>(this RouteHandlerBuilder builder)
        where TRequest : notnull =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
            if (request is null)
            {
                return await next(context);
            }

            var validator = context.HttpContext.RequestServices.GetService<IValidator<TRequest>>();
            if (validator is null)
            {
                return await next(context);
            }

            var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
            if (result.IsValid)
            {
                return await next(context);
            }

            var errors = result.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            return Results.ValidationProblem(errors);
        });
}
