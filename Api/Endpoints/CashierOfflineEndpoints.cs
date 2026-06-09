using System.Net;
using System.Security.Claims;
using Application.Services;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace Api.Endpoints;

// Endpoints that power the cashier panel's offline layer. None of them touch the
// existing /api/auth/* group — this is purely additive so the regular web app
// continues to behave exactly as before.
public static class CashierOfflineEndpoints
{
    public static void MapCashierOfflineEndpoints(this IEndpointRouteBuilder app)
    {
        var data = app.MapGroup("/api/cashier-offline").WithTags("Cashier Offline").RequireAuthorization();

        data.MapGet("/data", async (
            HttpContext httpContext,
            [FromServices] ICashierOfflineService service,
            [FromQuery] int? orderDays,
            [FromQuery] int? credentialDays) =>
        {
            var sub = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var userId))
                return Results.Unauthorized();

            var payload = await service.GetOfflineDataAsync(userId,
                orderHistoryDays: orderDays ?? 30,
                credentialLifetimeDays: credentialDays ?? 7);

            return payload is null ? Results.NotFound() : Results.Ok(payload);
        })
        .WithName("GetCashierOfflineData")
        .WithSummary("Return the full offline payload for the authenticated cashier");

        var products = app.MapGroup("/api/products").WithTags("Cashier Offline");

        // Thumbnail endpoint — serves the product's main image with a stable ETag
        // and a long Cache-Control so the browser cache + service worker can
        // re-use the response without re-downloading on every page load.
        //
        // The "max 200×200 / WebP" rendition described in the offline spec needs
        // an image library (ImageSharp/SkiaSharp); to keep this change package
        // additive we stream the source bytes through and rely on the cache
        // pipeline. Adding the resize step later is non-breaking.
        products.MapGet("/{productId:guid}/thumbnail", async (
            Guid productId,
            HttpContext httpContext,
            [FromServices] ApplicationDbContext db,
            [FromServices] Microsoft.AspNetCore.Hosting.IWebHostEnvironment env,
            CancellationToken ct) =>
        {
            var media = await db.Media
                .AsNoTracking()
                .Where(m => m.EntityType == EntityType.Product && m.EntityId == productId)
                .OrderByDescending(m => m.IsMain)
                .ThenBy(m => m.Order)
                .ThenBy(m => m.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (media is null) return Results.NotFound();

            var etag = $"\"{media.Id:N}-{media.Size}-{(media.UpdatedAt ?? media.CreatedAt).Ticks}\"";

            var ifNoneMatch = httpContext.Request.Headers[HeaderNames.IfNoneMatch].ToString();
            if (!string.IsNullOrEmpty(ifNoneMatch) && ifNoneMatch.Contains(etag))
            {
                httpContext.Response.Headers[HeaderNames.ETag] = etag;
                httpContext.Response.Headers[HeaderNames.CacheControl] = "public, max-age=2592000, immutable";
                return Results.StatusCode((int)HttpStatusCode.NotModified);
            }

            if (string.IsNullOrWhiteSpace(env.WebRootPath))
                return Results.NotFound();

            var absolute = Path.Combine(env.WebRootPath, media.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolute)) return Results.NotFound();

            httpContext.Response.Headers[HeaderNames.ETag] = etag;
            httpContext.Response.Headers[HeaderNames.CacheControl] = "public, max-age=2592000, immutable";

            var mime = string.IsNullOrWhiteSpace(media.MimeType) ? "image/jpeg" : media.MimeType;
            return Results.File(absolute, mime, enableRangeProcessing: false);
        })
        .WithName("GetProductThumbnail")
        .WithSummary("Serve the product's main image with cache-friendly headers");
    }
}
