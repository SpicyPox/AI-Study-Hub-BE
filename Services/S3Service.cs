using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

namespace AIStudyHub.Api.Services;

public class S3Service
{
    private readonly IAmazonS3? _s3;
    private readonly string _bucket;
    private readonly bool _isStub;

    public S3Service(IConfiguration config)
    {
        var aws = config.GetSection("Aws");
        _bucket = aws["BucketName"] ?? "local";
        _isStub = string.IsNullOrEmpty(aws["AccessKeyId"]) || aws["AccessKeyId"] == "YOUR_AWS_ACCESS_KEY";

        if (!_isStub)
        {
            _s3 = new AmazonS3Client(
                aws["AccessKeyId"],
                aws["SecretAccessKey"],
                RegionEndpoint.GetBySystemName(aws["Region"] ?? "ap-southeast-1"));
        }
    }

    public string GetPresignedUploadUrl(string key, string contentType)
    {
        if (_isStub)
            // Stub: return a local echo endpoint — frontend uploads here and we accept it
            return $"http://localhost:8080/api/stub/upload/{Uri.EscapeDataString(key)}";

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.AddMinutes(5),
            ContentType = contentType,
        };
        return _s3!.GetPreSignedURL(request);
    }

    public string BuildS3Key(Guid userId, string fileName) =>
        $"documents/{userId}/{Guid.NewGuid()}/{fileName}";

    public async Task DeleteObjectAsync(string key)
    {
        if (_isStub) return;
        await _s3!.DeleteObjectAsync(_bucket, key);
    }
}
