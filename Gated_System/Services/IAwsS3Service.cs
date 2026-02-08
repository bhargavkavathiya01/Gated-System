using Microsoft.AspNetCore.Http;
using Gated_System.Models;

namespace Gated_System.Services
{
    public interface IAwsS3Service
    {
        Task<ServiceResult<string>> UploadFileAsync(IFormFile file, string folderName);
    }
}
