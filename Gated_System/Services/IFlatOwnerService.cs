using Gated_System.Models;

namespace Gated_System.Services
{
    public interface IFlatOwnerService
    {
        Task<CreatedVisitorResult> CreateVisitorAsync(CreateVisitorDto dto);
        Task<object?> GetVisitorByIdAsync(int id);
        Task<int> CreatePGMembers(CreateFlatOwnerModel dto);
        Task<ServiceResult<IEnumerable<dynamic>>> GetPGMembersAsync(PGMemberRequest request);
        Task<ServiceResult<bool>> DeletePGMemberAsync(DeletePGRequest request);
    }
}
