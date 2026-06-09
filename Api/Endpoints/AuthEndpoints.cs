using Api.Filters;
using Application.Features.Auth;
using Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication")
            // Brute-force protection: per-IP fixed window (see Program.cs "auth" policy).
            .RequireRateLimiting("auth");

        group.MapPost("/register", async (
            [FromBody] RegisterRequest request,
            [FromServices] IAuthService authService,
            [FromServices] IValidator<RegisterRequest> validator) =>
        {
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors
                    .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
                    .ToDictionary(
                        failureGroup => failureGroup.Key,
                        failureGroup => failureGroup.ToArray());

                return Results.ValidationProblem(
                    errors,
                    title: "Validation Failed",
                    detail: "One or more validation errors occurred.");
            }

            var response = await authService.RegisterAsync(request);
            return response.Success
                ? Results.Ok(response)
                : Results.BadRequest(response);
        })
        .WithName("Register")
        .WithSummary("Register a new user")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .Produces<AuthResponse>(StatusCodes.Status400BadRequest)
        .ProducesValidationProblem();

        group.MapPost("/login", async (
            [FromBody] LoginRequest request,
            HttpContext httpContext,
            [FromServices] IAuthService authService,
            [FromServices] IValidator<LoginRequest> validator) =>
        {
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors
                    .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
                    .ToDictionary(
                        failureGroup => failureGroup.Key,
                        failureGroup => failureGroup.ToArray());

                return Results.ValidationProblem(
                    errors,
                    title: "Validation Failed",
                    detail: "One or more validation errors occurred.");
            }

            var response = await authService.LoginAsync(request, httpContext.Connection.RemoteIpAddress?.ToString());
            if (response.Success)
                return Results.Ok(response);

            // Return 403 with message for deactivated store so frontend can show the specific error
            if (response.Message != null && response.Message.Contains("deactivated", StringComparison.OrdinalIgnoreCase))
                return Results.Json(response, statusCode: StatusCodes.Status403Forbidden);

            return Results.Unauthorized();
        })
        .WithName("Login")
        .WithSummary("Login with email and password")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .ProducesValidationProblem();

        // Refresh access token using refresh token (rotates the refresh token)
        group.MapPost("/refresh", async (
            [FromBody] RefreshTokenRequest request,
            HttpContext httpContext,
            [FromServices] IAuthService authService) =>
        {
            var response = await authService.RefreshAsync(request, httpContext.Connection.RemoteIpAddress?.ToString());
            return response.Success
                ? Results.Ok(response)
                : Results.Unauthorized();
        })
        .WithName("RefreshToken")
        .WithSummary("Exchange a refresh token for a new access + refresh token pair")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // Send email confirmation (anonymous or authenticated)
        group.MapPost("/send-email-confirmation", async (
            [FromBody] string email,
            [FromServices] IAuthService authService) =>
        {
            var response = await authService.SendEmailConfirmationAsync(email);
            return response.Success ? Results.Ok(response) : Results.BadRequest(response);
        })
        .WithName("SendEmailConfirmation")
        .WithSummary("Send email confirmation link");

        // Confirm email
        group.MapPost("/confirm-email", async (
            [FromBody] ConfirmEmailRequest request,
            [FromServices] IAuthService authService) =>
        {
            var response = await authService.ConfirmEmailAsync(request);
            return response.Success ? Results.Ok(response) : Results.BadRequest(response);
        })
        .WithName("ConfirmEmail")
        .WithSummary("Confirm user email via token");

        // Forgot password
        group.MapPost("/forgot-password", async (
            [FromBody] ForgotPasswordRequest request,
            [FromServices] IAuthService authService,
            [FromServices] IValidator<ForgotPasswordRequest> validator) =>
        {
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors
                    .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
                    .ToDictionary(
                        failureGroup => failureGroup.Key,
                        failureGroup => failureGroup.ToArray());

                return Results.ValidationProblem(
                    errors,
                    title: "Validation Failed",
                    detail: "One or more validation errors occurred.");
            }

            var response = await authService.ForgotPasswordAsync(request);
            return response.Success ? Results.Ok(response) : Results.BadRequest(response);
        })
        .WithName("ForgotPassword")
        .WithSummary("Request password reset link")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .Produces<AuthResponse>(StatusCodes.Status400BadRequest)
        .ProducesValidationProblem();

        // Reset password
        group.MapPost("/reset-password", async (
            [FromBody] ResetPasswordRequest request,
            [FromServices] IAuthService authService) =>
        {
            var response = await authService.ResetPasswordAsync(request);
            return response.Success ? Results.Ok(response) : Results.BadRequest(response);
        })
        .AddEndpointFilter<ValidationFilter<ResetPasswordRequest>>()
        .WithName("ResetPassword")
        .WithSummary("Reset password using token")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .Produces<AuthResponse>(StatusCodes.Status400BadRequest)
        .ProducesValidationProblem();

        // Change password - requires authentication
        group.MapPost("/change-password", async (
            [FromBody] ChangePasswordRequest request,
            HttpContext httpContext,
            [FromServices] IAuthService authService) =>
        {
            var sub = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var userId))
            {
                return Results.Unauthorized();
            }

            var response = await authService.ChangePasswordAsync(userId, request);
            return response.Success ? Results.Ok(response) : Results.BadRequest(response);
        })
        .AddEndpointFilter<ValidationFilter<ChangePasswordRequest>>()
        .RequireAuthorization()
        .WithName("ChangePassword")
        .WithSummary("Change password for authenticated user")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .Produces<AuthResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .ProducesValidationProblem();

        // Logout - requires authentication.
        group.MapPost("/logout", async (
            [FromBody] RefreshTokenRequest? request,
            HttpContext httpContext,
            [FromServices] IAuthService authService) =>
        {
            var sub = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var userId))
            {
                return Results.Unauthorized();
            }

            if (!string.IsNullOrWhiteSpace(request?.RefreshToken))
            {
                await authService.RevokeRefreshTokenAsync(
                    request!.RefreshToken,
                    httpContext.Connection.RemoteIpAddress?.ToString());
            }

            return Results.Ok(new AuthResponse { Success = true, Message = "LoggedOut" });
        })
        .RequireAuthorization()
        .WithName("Logout")
        .WithSummary("Logout")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .Produces<AuthResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
