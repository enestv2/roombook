using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;

namespace Roombook.Api.Localization;

public static class LocalizedProblemDetails
{
    private const string ErrorCodesItemKey = "Roombook.ErrorCodes";

    public static void AddFieldCode(HttpContext context, string field, string code)
    {
        if (context.Items[ErrorCodesItemKey] is not Dictionary<string, string[]> codes)
        {
            codes = new Dictionary<string, string[]>();
            context.Items[ErrorCodesItemKey] = codes;
        }
        codes[field] = new[] { code };
    }

    public static ValidationProblemDetails FromModelState(
        HttpContext context,
        ModelStateDictionary modelState,
        IStringLocalizer<ApiMessages> messages)
    {
        var errors = modelState.ToDictionary(
            entry => entry.Key,
            entry => entry.Value!.Errors.Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                ? messages[ApiErrorCodes.InvalidInput].Value
                : error.ErrorMessage).ToArray());
        var suppliedCodes = context.Items[ErrorCodesItemKey] as Dictionary<string, string[]>;
        var codes = modelState.ToDictionary(
            entry => entry.Key,
            entry => suppliedCodes?.TryGetValue(entry.Key, out var fieldCodes) == true
                ? fieldCodes
                : entry.Value!.Errors.Select(_ => ApiErrorCodes.InvalidInput).ToArray());
        var problem = new ValidationProblemDetails(errors.ToDictionary(x => x.Key, x => x.Value))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = messages[ApiErrorCodes.Validation].Value
        };
        AddExtensions(problem, context, ApiErrorCodes.Validation, codes);
        return problem;
    }

    public static ValidationProblemDetails FromErrors(
        HttpContext context,
        IReadOnlyDictionary<string, string[]> errors,
        IReadOnlyDictionary<string, string[]> codes,
        IStringLocalizer<ApiMessages> messages)
    {
        var problem = new ValidationProblemDetails(errors.ToDictionary(x => x.Key, x => x.Value))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = messages[ApiErrorCodes.Validation].Value
        };
        AddExtensions(problem, context, ApiErrorCodes.Validation, codes);
        return problem;
    }

    public static void AddExtensions(
        ProblemDetails problem,
        HttpContext context,
        string code,
        IReadOnlyDictionary<string, string[]>? errorCodes = null)
    {
        problem.Extensions["code"] = code;
        if (errorCodes is not null) problem.Extensions["errorCodes"] = errorCodes;
        problem.Extensions["correlationId"] = context.Items[CorrelationIdMiddleware.ItemKey]?.ToString()
            ?? context.TraceIdentifier;
    }
}
