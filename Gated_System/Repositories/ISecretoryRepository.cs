using Gated_System.Models;

namespace Gated_System.Repositories
{
    public interface ISecretoryRepository
    {
        Task<int> CreateCommitteeRepoAsync(CreateCommitteeModel model);
        Task<int> UploadSocietyImageAsync(int propertyId, string imageUrl, string? imageTitle, int uploadedBy);
        Task<IEnumerable<SocietyImageResponseModel>> GetSocietyImagesAsync(int propertyId);
    }
}
