using ShiaiManager.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ShiaiManager.Api.Controllers;

/// <summary>
/// Builds ProblemDetails responses that carry a <c>messageKey</c> extension for frontend localization.
/// </summary>
internal static class LocalizedProblemResults
{
    /// <summary>ProblemDetails extension that holds the frontend translation key.</summary>
    public const string MessageKeyExtension = "messageKey";

    /// <summary>
    /// Returns a 400 validation problem for <paramref name="field"/> with a localizable message.
    /// </summary>
    public static ActionResult LocalizedValidationProblem(
        this ControllerBase controller,
        string field,
        LocalizedMessage message)
    {
        controller.ModelState.AddModelError(field, message.Text);
        var result = (ObjectResult)controller.ValidationProblem(controller.ModelState);
        ((ProblemDetails)result.Value!).Extensions[MessageKeyExtension] = message.Key;
        return result;
    }

    /// <summary>
    /// Returns a 409 conflict with a localizable detail message.
    /// </summary>
    public static ConflictObjectResult LocalizedConflict(
        this ControllerBase controller,
        string title,
        LocalizedMessage message)
    {
        var problem = new ProblemDetails
        {
            Title = title,
            Detail = message.Text,
            Status = StatusCodes.Status409Conflict
        };
        problem.Extensions[MessageKeyExtension] = message.Key;
        return controller.Conflict(problem);
    }
}
