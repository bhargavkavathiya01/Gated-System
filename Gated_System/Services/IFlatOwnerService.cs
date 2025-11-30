using Gated_System.Models;

namespace Gated_System.Services
{
    public interface IFlatOwnerService
    {
        Task<CreatedVisitorResult> CreateVisitorAsync(CreateVisitorDto dto);
        Task<object?> GetVisitorByIdAsync(int id);
        Task<int> CreatePGMembers(CreateFlatOwnerModel dto);
    }
}
