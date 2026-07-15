using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Healthcare.Presentation.API.Filters;

/// <summary>
/// Action filter for automatic model validation.
/// </summary>
/// <remarks>
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
            // Prefer ValidationProblemDetails so SPA parseApiError can map field → messages.
            // (Flat string[] errors force generic "HTTP 400" / banner-only UX.)
            context.Result = new BadRequestObjectResult(
                new ValidationProblemDetails(context.ModelState)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "One or more validation errors occurred.",
                });
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        // Nothing to do after action execution
    }
}