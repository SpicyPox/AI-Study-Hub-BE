using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace AIStudyHub.Api.Services;

public record CloudinaryUploadResult(string PublicId, string SecureUrl);

public class CloudinaryService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryService(IConfiguration config)
    {
        var cloudinary = config.GetSection("Cloudinary");
        var cloudName = cloudinary["CloudName"];
        var apiKey = cloudinary["ApiKey"];
        var apiSecret = cloudinary["ApiSecret"];

        if (string.IsNullOrWhiteSpace(cloudName) ||
            string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(apiSecret))
        {
            throw new InvalidOperationException("Cloudinary configuration is missing CloudName, ApiKey, or ApiSecret.");
        }

        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account);
    }

    public async Task<CloudinaryUploadResult> UploadDocumentAsync(IFormFile file, Guid userId)
    {
        var publicId = BuildPublicId(userId, file.FileName);

        await using var stream = file.OpenReadStream();
        var uploadParams = new RawUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            PublicId = publicId,
            Overwrite = false
        };

        var result = await _cloudinary.UploadAsync(uploadParams);
        if (result.Error is not null)
            throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");

        return new CloudinaryUploadResult(result.PublicId, result.SecureUrl?.ToString() ?? string.Empty);
    }

    public async Task DeleteDocumentAsync(string publicId)
    {
        if (string.IsNullOrWhiteSpace(publicId)) return;

        var deleteParams = new DeletionParams(publicId)
        {
            ResourceType = ResourceType.Raw
        };

        var result = await _cloudinary.DestroyAsync(deleteParams);
        if (result.Error is not null)
            throw new InvalidOperationException($"Cloudinary delete failed: {result.Error.Message}");
    }

    private static string BuildPublicId(Guid userId, string fileName)
    {
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName).TrimStart('.');
        var safeName = string.Join("_", nameWithoutExtension.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        var suffix = string.IsNullOrWhiteSpace(extension) ? string.Empty : $".{extension}";

        return $"documents/{userId}/{Guid.NewGuid()}/{safeName}{suffix}";
    }
}
