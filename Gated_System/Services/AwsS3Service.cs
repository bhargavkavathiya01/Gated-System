using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;
using Gated_System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Gated_System.Services
{
    public class AwsS3Service : IAwsS3Service
    {
        private readonly IConfiguration _configuration;
        private readonly string _bucketName;
        private readonly IAmazonS3 _s3Client;

        public AwsS3Service(IConfiguration configuration)
        {
            _configuration = configuration;
            _bucketName = _configuration["AWS:BucketName"];
            var accessKey = _configuration["AWS:AccessKey"];
            var secretKey = _configuration["AWS:SecretKey"];
            var region = _configuration["AWS:Region"];

            _s3Client = new AmazonS3Client(accessKey, secretKey, RegionEndpoint.GetBySystemName(region));
        }

        public async Task<ServiceResult<string>> UploadFileAsync(IFormFile file, string folderName)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return ServiceResult<string>.Fail("File is empty");
                }

                var fileTransferUtility = new TransferUtility(_s3Client);
                var fileName = $"{folderName}/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

                using (var newMemoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(newMemoryStream);
                    var uploadRequest = new TransferUtilityUploadRequest
                    {
                        InputStream = newMemoryStream,
                        Key = fileName,
                        BucketName = _bucketName,
                        //CannedACL = S3CannedACL.PublicRead
                    };

                    await fileTransferUtility.UploadAsync(uploadRequest);
                }

                var fileUrl = $"https://{_bucketName}.s3.{_configuration["AWS:Region"]}.amazonaws.com/{fileName}";
                return ServiceResult<string>.Success(fileUrl);
            }
            catch (Exception ex)
            {
                return ServiceResult<string>.Fail($"Error uploading file: {ex.Message}");
            }
        }
    }
}
