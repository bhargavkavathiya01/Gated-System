using Gated_System.Models;
using static Gated_System.Models.QRModel;

namespace Gated_System.Services
{
    public interface IFlatOwnerService
    {
        Task<CreatedVisitorResult> CreateVisitorAsync(CreateVisitorDto dto);
        Task<object?> GetVisitorByIdAsync(int id);
        Task<int> CreatePGMembers(CreateFlatOwnerModel dto);
        Task<ServiceResult<IEnumerable<dynamic>>> GetPGMembersAsync(PGMemberRequest request);
        Task<ServiceResult<bool>> DeletePGMemberAsync(DeletePGRequest request);
        Task<ServiceResult<IEnumerable<dynamic>>> GetHistoryAsync(QRHistoryRequest request);
        Task<ServiceResult<bool>> RevokeAsync(RevokeQRRequest request);
        Task RegisterDeviceTokenAsync(UserDeviceTokenModel dto);
        Task ApproveVisitorRequestAsync(VisitorApprovalRequest req);
    }
}
