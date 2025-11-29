using Gated_System.Models;

namespace Gated_System.Services
{
    public interface IBuilderService
    {
        Task<int> CreateSecretaryAsync(CreateSecretaryModel dto);
        Task<int> CreateFlatOwnerAsync(CreateFlatOwnerModel dto);
        Task<IEnumerable<PropertyViewModel>> GetPropertiesByBuilderIdAsync(int builderId);
    }
}
