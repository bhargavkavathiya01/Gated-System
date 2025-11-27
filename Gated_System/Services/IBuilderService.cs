using Gated_System.Models;

namespace Gated_System.Services
{
    public interface IBuilderService
    {
        Task<int> CreateSecretaryAsync(CreateSecretaryModel dto);
    }
}
