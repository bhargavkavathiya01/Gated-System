using Gated_System.Models;

namespace Gated_System.Services
{
    public interface ISecretoryService
    {
        Task<int> CreateCommitteeAsync(CreateCommitteeModel dto);
        Task<List<int>> UploadSocietyImagesAsync(SocietyImageUploadModel dto, List<string> imageUrls);
        Task<IEnumerable<SocietyImageResponseModel>> GetSocietyImagesAsync(int propertyId);
    }
}
