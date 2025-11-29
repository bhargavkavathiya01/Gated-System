using Gated_System.Models;

namespace Gated_System.Repositories
{
    public interface IBuilderRepository
    {
        Task<int> CreateSecretaryRepoAsync(CreateSecretaryModel model);
        Task<int> CreateFlatOwnerRepoAsync(CreateFlatOwnerModel model);
        Task<IEnumerable<PropertyViewModel>> GetPropertiesByBuilderIdAsync(int builderId);
    }
}
