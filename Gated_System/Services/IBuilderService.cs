using Gated_System.Models;

namespace Gated_System.Services
{
    public interface IBuilderService
    {
        Task<int> CreateSecretaryAsync(CreateSecretaryModel dto);
        Task<int> CreateFlatOwnerAsync(CreateFlatOwnerModel dto);
        Task<IEnumerable<PropertyViewModel>> GetPropertiesByBuilderIdAsync(int builderId);
        Task<IEnumerable<RoleModel>> GetRolesAsync();
        Task<List<UserListResponseModel>> GetAllUsersAsync();
        Task<UserResponseModel?> GetUserByEmailOrPhoneAsync(string user);
        Task<ServiceResult<PropertyMemberDetailsResponse>> GetMemberDetailsAsync(PropertyMemberRequest request);
        Task<int> CreateFlatOwnerRequestAsync(CreateFlatOwnerRequestModel dto);
        Task<IEnumerable<FlatOwnerRequestResponseModel>> GetAllFlatOwnerRequestsAsync(string? status = null);

        // Security approval flow
        Task<int> CreateSecurityRequestAsync(CreateSecurityRequestModel dto);
        Task<IEnumerable<SecurityRequestResponseModel>> GetAllSecurityRequestsAsync(string? status = null);
        Task<SecurityRequestResponseModel?> GetSecurityRequestByIdAsync(int requestId);
        Task<bool> ApproveSecurityRequestAsync(ApproveSecurityRequestModel dto, int approvedBy);
    }
}
