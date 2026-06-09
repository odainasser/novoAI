using System.Text.Json.Serialization;

namespace Web.Services;

public interface IMediaService
{
    Task<MediaDto> UploadMediaAsync(Guid entityId, string entityType, Stream fileStream, string fileName, string contentType, string collectionName = "default");
    Task<IEnumerable<MediaDto>> GetMediaForEntityAsync(Guid entityId, string entityType, string? collectionName = null);
    Task DeleteMediaAsync(Guid mediaId);
    Task SetMainMediaAsync(Guid mediaId, Guid entityId, string entityType, string collectionName);
    string GetMediaUrl(MediaDto media);
}

public class MediaDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    
    [JsonPropertyName("mimeType")]
    public string ContentType { get; set; } = string.Empty;
    
    public long Size { get; set; }
    
    [JsonPropertyName("path")]
    public string Url { get; set; } = string.Empty;
    
    public string CollectionName { get; set; } = string.Empty;
    public bool IsMain { get; set; }
    public DateTime CreatedAt { get; set; }
}
