using Application.Services;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Domain.Enums;
using Application.Common.Interfaces;

namespace Infrastructure.Services;

public class MediaService : IMediaService
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly IAppConfiguration _appConfig;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MediaService(ApplicationDbContext context, IWebHostEnvironment environment, IAppConfiguration appConfig, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _environment = environment;
        _appConfig = appConfig;
        _httpContextAccessor = httpContextAccessor;
    }

    // Security: Allowed file extensions and MIME types
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".bmp", ".ico"
    };

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp", "image/svg+xml", "image/bmp", "image/x-icon", "image/vnd.microsoft.icon"
    };

    // Security: Maximum file size (10 MB)
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public async Task<Media> UploadMediaAsync(Guid entityId, EntityType entityType, Stream fileStream, string fileName, string contentType, string collectionName = "default")
    {
        if (string.IsNullOrWhiteSpace(_environment.WebRootPath))
        {
            throw new InvalidOperationException("WebRootPath is not configured. Ensure the API project has a wwwroot folder.");
        }

        // Capture file size before any stream operations (more reliable)
        var fileSize = fileStream.Length;

        // Security: Validate file size
        if (fileSize > MaxFileSizeBytes)
        {
            throw new InvalidOperationException($"File size exceeds maximum allowed size of {MaxFileSizeBytes / (1024 * 1024)} MB.");
        }

        if (fileSize == 0)
        {
            throw new InvalidOperationException("Cannot upload an empty file.");
        }

        // Security: Validate file extension
        var fileExtension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(fileExtension) || !AllowedExtensions.Contains(fileExtension))
        {
            throw new InvalidOperationException($"File type '{fileExtension}' is not allowed. Allowed types: {string.Join(", ", AllowedExtensions)}");
        }

        // Security: Validate MIME type
        if (string.IsNullOrEmpty(contentType) || !AllowedMimeTypes.Contains(contentType))
        {
            throw new InvalidOperationException($"Content type '{contentType}' is not allowed.");
        }

        // Security: Sanitize filename to prevent path traversal attacks
        var sanitizedFileName = Path.GetFileName(fileName);
        if (string.IsNullOrEmpty(sanitizedFileName) || sanitizedFileName.Contains(".."))
        {
            throw new InvalidOperationException("Invalid file name.");
        }

        var entityDir = entityType.ToString().ToLower();
        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", entityDir, entityId.ToString());
        
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var uniqueFileName = $"{Guid.NewGuid()}_{sanitizedFileName}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        // Ensure stream position is at the beginning before copying
        if (fileStream.CanSeek && fileStream.Position != 0)
        {
            fileStream.Position = 0;
        }

        // Write file to disk
        using (var outputStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
        {
            await fileStream.CopyToAsync(outputStream);
            await outputStream.FlushAsync();
        }

        // Verify file was saved correctly
        var savedFileInfo = new FileInfo(filePath);
        if (!savedFileInfo.Exists)
        {
            throw new InvalidOperationException("Failed to save the uploaded file - file does not exist after write.");
        }

        if (savedFileInfo.Length == 0)
        {
            // Clean up the empty file
            try { File.Delete(filePath); } catch { /* ignore cleanup errors */ }
            throw new InvalidOperationException("Failed to save the uploaded file - file is empty after write. The upload stream may have been consumed before saving.");
        }

        var media = new Media
        {
            Id = Guid.NewGuid(),
            EntityId = entityId,
            EntityType = entityType,
            CollectionName = collectionName,
            Name = Path.GetFileNameWithoutExtension(fileName),
            FileName = uniqueFileName,
            MimeType = contentType,
            Disk = "local",
            Path = Path.Combine("uploads", entityDir, entityId.ToString(), uniqueFileName).Replace("\\", "/"),
            Size = savedFileInfo.Length, // Use actual saved file size
            Order = 0
        };

        _context.Media.Add(media);
        await _context.SaveChangesAsync();

        return media;
    }

    public async Task<IEnumerable<Media>> GetMediaForEntityAsync(Guid entityId, EntityType entityType, string? collectionName = null)
    {
        var query = _context.Media.Where(m => m.EntityId == entityId && m.EntityType == entityType);
        
        if (!string.IsNullOrEmpty(collectionName))
        {
            query = query.Where(m => m.CollectionName == collectionName);
        }

        return await query.OrderBy(m => m.Order).ThenBy(m => m.CreatedAt).ToListAsync();
    }

    public async Task DeleteMediaAsync(Guid mediaId)
    {
        var media = await _context.Media.FindAsync(mediaId);
        if (media != null)
        {
            if (!string.IsNullOrWhiteSpace(_environment.WebRootPath))
            {
                var filePath = Path.Combine(_environment.WebRootPath, media.Path);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }

            _context.Media.Remove(media);
            await _context.SaveChangesAsync();
        }
    }

    public async Task SetMainMediaAsync(Guid mediaId, Guid entityId, EntityType entityType, string collectionName)
    {
        var mediaItems = await _context.Media
            .Where(m => m.EntityId == entityId && m.EntityType == entityType && m.CollectionName == collectionName)
            .ToListAsync();

        foreach (var item in mediaItems)
        {
            item.IsMain = item.Id == mediaId;
        }

        await _context.SaveChangesAsync();
    }

    public string GetMediaUrl(Media media)
    {
        if (media.Path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return media.Path;

        // Get the API's base URL dynamically from the current request context
        // This ensures media URLs point to the API server where files are hosted
        var request = _httpContextAccessor.HttpContext?.Request;
        string baseUrl;
        if (request != null)
        {
            baseUrl = $"{request.Scheme}://{request.Host}";
        }
        else
        {
            // Fallback to config if no HTTP context available
            baseUrl = _appConfig.GetAppUrl().TrimEnd('/');
        }

        // Ensure path starts with a forward slash for proper URL construction
        var trimmedPath = media.Path.TrimStart('/');
        return $"{baseUrl}/{trimmedPath}";
    }
}
