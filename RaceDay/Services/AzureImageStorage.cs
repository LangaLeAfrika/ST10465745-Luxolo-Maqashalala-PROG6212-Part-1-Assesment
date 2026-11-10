using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDay.Services;

public sealed class AzureImageStorage(IConfiguration configuration) : IImageStorage
{
    // Construct lazily so normal API features work before Azure is configured.
    private BlobContainerClient Container()
    {
        var connection = configuration["AzureBlobStorage:ConnectionString"];
        var name = configuration["AzureBlobStorage:ContainerName"] ?? "raceday-images";
        if (string.IsNullOrWhiteSpace(connection)) throw new ImageStorageUnavailableException();
        try { return new BlobContainerClient(connection, name); }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        { throw new ImageStorageUnavailableException(); }
    }

    public async Task UploadAsync(string path, byte[] bytes, string contentType, CancellationToken cancellationToken)
    {
        var container = Container();
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        // Refuse a pre-existing public container instead of silently exposing profile photos.
        var properties = await container.GetPropertiesAsync(cancellationToken: cancellationToken);
        if (properties.Value.PublicAccess != PublicAccessType.None)
            throw new ImageStorageUnavailableException();
        await container.GetBlobClient(path).UploadAsync(new BinaryData(bytes), new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
        }, cancellationToken);
    }

    public async Task<StoredImage?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var response = await Container().GetBlobClient(path).DownloadContentAsync(cancellationToken);
            return new StoredImage(response.Value.Content.ToArray(), response.Value.Details.ContentType);
        }
        catch (RequestFailedException ex) when (ex.Status == 404) { return null; }
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken) =>
        await Container().GetBlobClient(path).DeleteIfExistsAsync(cancellationToken: cancellationToken);
}
