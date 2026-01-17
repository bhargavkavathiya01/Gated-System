using Gated_System.Helpers;
using Gated_System.Models;
using Gated_System.Repositories;

namespace Gated_System.Services
{
    public class BuilderService : IBuilderService
    {
        private readonly IBuilderRepository _repo;
        private readonly IFlatOwnerRepository _flatownerRepo;

        public BuilderService(IBuilderRepository repo , IFlatOwnerRepository flatownerRepo)
        {
            _repo = repo;
            _flatownerRepo = flatownerRepo;
        }

        public async Task<IEnumerable<RoleModel>> GetRolesAsync()
        {
            return await _repo.GetAllRolesAsync();
        }

        public async Task<int> CreateSecretaryAsync(CreateSecretaryModel dto)
        {
            // Basic validation
            if (dto.PropertyId <= 0) throw new ApplicationException("Invalid PropertyId.");
            if (dto.UserId <= 0) throw new ApplicationException("Invalid UserId.");
            if (dto.RoleId <= 0) throw new ApplicationException("Invalid RoleId.");

            var id = await _repo.CreateSecretaryRepoAsync(dto);
            return id;
        }

        public async Task<int> CreateFlatOwnerAsync(CreateFlatOwnerModel dto)
        {
            // Basic validation
            if (dto.PropertyId <= 0) throw new ApplicationException("Invalid PropertyId.");
            if (dto.BuildingId <= 0) throw new ApplicationException("Invalid BuildingId.");
            if (dto.FlatNo == null) throw new ApplicationException("Invalid FlatNo.");
            if (dto.UserId <= 0) throw new ApplicationException("Invalid UserId.");
            if (dto.RoleId <= 0) throw new ApplicationException("Invalid RoleId.");

            var id = await _repo.CreateFlatOwnerRepoAsync(dto);

            // Create a permanent QR for this PG member (mirror flat owner behavior)
            try
            {
                var token = QrHelper.GenerateToken();

                var visitorPayload = new
                {
                    visitorname = "Self",
                    phone = "",
                    purpose = "Permanent QR for flat owner",
                    propertyid = dto.PropertyId,
                    buildingid = dto.BuildingId,
                    flatid = dto.FlatNo,
                    userid = dto.UserId,
                    qrcode = token,
                    qr_type = "unlimited",
                    flatownerid = dto.UserId
                };

                await _flatownerRepo.CreateVisitorRequestAsync(visitorPayload);
            }
            catch (Exception ex)
            {
                throw new ApplicationException("PG member created but failed to create permanent QR: " + ex.Message);
            }

            return id;
        }

        public async Task<IEnumerable<PropertyViewModel>> GetPropertiesByBuilderIdAsync(int builderId)
        {
            if (builderId <= 0)
                throw new ApplicationException("Invalid builderId.");

            return await _repo.GetPropertiesByBuilderIdAsync(builderId);
        }

        public async Task<List<UserListResponseModel>> GetAllUsersAsync()
        {
            return await _repo.GetAllUsersAsync();
        }
        public async Task<UserResponseModel?> GetUserByEmailOrPhoneAsync(string user)
        {
            return await _repo.GetUserByEmailOrPhoneAsync(user);
        }

        public async Task<ServiceResult<PropertyMemberDetailsResponse>> GetMemberDetailsAsync(PropertyMemberRequest request)
        {
            try
            {
                if (request.PropertyId <= 0)
                    return ServiceResult<PropertyMemberDetailsResponse>.Fail("Invalid Property ID");

                var data = await _repo.GetPropertyMemberDetailsAsync(request);

                if (data == null)
                    return ServiceResult<PropertyMemberDetailsResponse>.Fail("No data found for the specified property");

                return ServiceResult<PropertyMemberDetailsResponse>.Success(data, "Property member details fetched successfully");
            }
            catch (Exception ex)
            {
                return ServiceResult<PropertyMemberDetailsResponse>.Fail(ex.Message);
            }
        }

        public async Task<int> CreateFlatOwnerRequestAsync(CreateFlatOwnerRequestModel dto)
        {
            // Basic validation
            if (dto.PropertyId <= 0) throw new ApplicationException("Invalid PropertyId.");
            if (dto.BuildingId <= 0) throw new ApplicationException("Invalid BuildingId.");
            if (dto.FlatNo == null) throw new ApplicationException("Invalid FlatNo.");
            if (dto.UserId <= 0) throw new ApplicationException("Invalid UserId.");
            if (dto.RoleId <= 0) throw new ApplicationException("Invalid RoleId.");

            var id = await _repo.CreateFlatOwnerRequestAsync(dto);
            return id;
        }

        public async Task<IEnumerable<FlatOwnerRequestResponseModel>> GetAllFlatOwnerRequestsAsync(string? status = null)
        {
            return await _repo.GetAllFlatOwnerRequestsAsync(status);
        }
    }
}
