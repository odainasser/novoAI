using Domain.Entities;
using Domain.Enums;

namespace Application.Services;

public interface IMediaService
{
    Task<Media> UploadMediaAsync(Guid entityId, EntityType entityType, Stream fileStream, string fileName, string contentType, string collectionName = "default");
    Task<IEnumerable<Media>> GetMediaForEntityAsync(Guid entityId, EntityType entityType, string? collectionName = null);
    Task DeleteMediaAsync(Guid mediaId);
    Task SetMainMediaAsync(Guid mediaId, Guid entityId, EntityType entityType, string collectionName);
    string GetMediaUrl(Media media);
}
