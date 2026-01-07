using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Healthcare.Presentation.API.Responses;

namespace Healthcare.Presentation.API.Filters;

/// <summary>
/// Action filter for automatic model validation.
/// </summary>
/// <remarks>
/// Design Pattern: Filter Pattern (Decorator-like)
/// 
/// This filter:
/// - Runs before controller action
/// - Checks ModelState for validation errors
/// - Returns 400 Bad Request with error details
/// - Prevents invalid requests from reaching controllers
/// 
/// Benefits:
/// - Centralized validation handling
/// - Consistent error responses
/// - Less boilerplate in controllers
/// </remarks>
public sealed class ValidationFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors)
                .Select(x => x.ErrorMessage)
                .ToList();

            var response = ApiResponse.ErrorResponse(
                errors,
                "Validation failed");

            context.Result = new BadRequestObjectResult(response);
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        // Nothing to do after action execution
    }
}