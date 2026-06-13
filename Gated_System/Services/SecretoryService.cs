using Gated_System.Models;
using Gated_System.Repositories;

namespace Gated_System.Services
{
    public class SecretoryService : ISecretoryService
    {
        private readonly ISecretoryRepository _repo;

        public SecretoryService(ISecretoryRepository repo)
        {
            _repo = repo;
        }

        public async Task<int> CreateCommitteeAsync(CreateCommitteeModel dto)
        {
            // Basic validation
            if (dto.PropertyId <= 0) throw new ApplicationException("Invalid PropertyId.");
            if (dto.UserId <= 0) throw new ApplicationException("Invalid UserId.");
            if (dto.RoleId <= 0) throw new ApplicationException("Invalid RoleId.");

            var id = await _repo.CreateCommitteeRepoAsync(dto);
            return id;
        }

        public async Task<List<int>> UploadSocietyImagesAsync(SocietyImageUploadModel dto, List<string> imageUrls)
        {
            if (dto.PropertyId <= 0) throw new ApplicationException("Invalid PropertyId.");
            if (imageUrls == null || imageUrls.Count == 0) throw new ApplicationException("No images to upload.");

            var ids = new List<int>();
            foreach (var url in imageUrls)
            {
                var id = await _repo.UploadSocietyImageAsync(dto.PropertyId, url, dto.ImageTitle, dto.UploadedBy);
                ids.Add(id);
            }
            return ids;
        }

        public async Task<IEnumerable<SocietyImageResponseModel>> GetSocietyImagesAsync(int propertyId)
        {
            if (propertyId <= 0) throw new ApplicationException("Invalid PropertyId.");
            return await _repo.GetSocietyImagesAsync(propertyId);
        }
    }
}
