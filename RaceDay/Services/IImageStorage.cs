namespace RaceDay.Services;

public record StoredImage(byte[] Bytes, string ContentType);

public interface IImageStorage
{
    Task UploadAsync(string path, byte[] bytes, string contentType, CancellationToken cancellationToken);
    Task<StoredImage?> ReadAsync(string path, CancellationToken cancellationToken);
    Task DeleteAsync(string path, CancellationToken cancellationToken);
}

public sealed class ImageStorageUnavailableException : Exception
{
    public ImageStorageUnavailableException() : base("Image storage is unavailable.") { }
}
