using Gated_System.Models;

namespace Gated_System.Repositories
{
    public interface IBuilderRepository
    {
        Task<int> CreateSecretaryRepoAsync(CreateSecretaryModel model);
        Task<int> CreateFlatOwnerRepoAsync(CreateFlatOwnerModel model);
        Task<IEnumerable<PropertyViewModel>> GetPropertiesByBuilderIdAsync(int builderId);
        Task<IEnumerable<RoleModel>> GetAllRolesAsync();
        Task<List<UserListResponseModel>> GetAllUsersAsync();
        Task<UserResponseModel?> GetUserByEmailOrPhoneAsync(string user);
        Task<PropertyMemberDetailsResponse?> GetPropertyMemberDetailsAsync(PropertyMemberRequest request);
        Task<int> CreateFlatOwnerRequestAsync(CreateFlatOwnerRequestModel model);
        Task<IEnumerable<FlatOwnerRequestResponseModel>> GetAllFlatOwnerRequestsAsync(string? status = null);
        Task<bool> UpdateFlatOwnerRequestStatusAsync(int requestId, string status, int approvedBy, string? rejectionReason);
        Task<FlatOwnerRequestResponseModel?> GetFlatOwnerRequestByIdAsync(int requestId);

        // Security approval flow
        Task<int> CreateSecurityRequestAsync(CreateSecurityRequestModel model);
        Task<IEnumerable<SecurityRequestResponseModel>> GetAllSecurityRequestsAsync(string? status = null);
        Task<SecurityRequestResponseModel?> GetSecurityRequestByIdAsync(int requestId);
        Task<bool> UpdateSecurityRequestStatusAsync(int requestId, string status, int approvedBy, string? rejectionReason);

        Task<string> GetPropertyVerificationStatusAsync(int propertyId);
    }
}
