using Gated_System.Helpers;
using Gated_System.Models;
using Gated_System.Repositories;

namespace Gated_System.Services
{
    public class SuperAdminService :ISuperAdminService
    {
        private readonly ISuperAdminRepository _repo;
        private readonly IBuilderRepository _builderRepo;
        private readonly IFlatOwnerRepository _flatownerRepo;

        public SuperAdminService(ISuperAdminRepository repo, IBuilderRepository builderRepo, IFlatOwnerRepository flatownerRepo)
        {
            _repo = repo;
            _builderRepo = builderRepo;
            _flatownerRepo = flatownerRepo;
        }

        public async Task<IEnumerable<AdminPropertyViewModel>> GetAllPropertiesAsync()
        {
            var result = await _repo.GetAllPropertiesAsync();
            return result ?? Enumerable.Empty<AdminPropertyViewModel>();
        }

        public async Task<IEnumerable<AdminPropertyViewModel>> GetAllPropertiesByStatusAsync(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) throw new ApplicationException("Status required.");
            var result = await _repo.GetAllPropertiesByStatusAsync(status);
            return result ?? Enumerable.Empty<AdminPropertyViewModel>();
        }

        public async Task<string> UpdatePropertyVerificationAsync(UpdatePropertyVerificationModel dto)
        {
            if (dto == null) throw new ApplicationException("Request body is required.");
            if (dto.PropertyId <= 0) throw new ApplicationException("Invalid property id.");
            if (string.IsNullOrWhiteSpace(dto.IsVerified)) throw new ApplicationException("Verification status is required.");

            // normalize status value (optional)
            dto.IsVerified = dto.IsVerified.Trim();

            // call repository to perform update
            return await _repo.UpdatePropertyVerificationRepoAsync(dto);
        }

        public async Task<bool> ApproveFlatOwnerRequestAsync(ApproveFlatOwnerRequestModel dto, int adminId)
        {
            if (dto.RequestId <= 0) throw new ApplicationException("Invalid Request ID.");
            if (dto.Action != "Approved" && dto.Action != "Rejected")
                throw new ApplicationException("Action must be 'Approved' or 'Rejected'.");

            if (dto.Action == "Rejected" && string.IsNullOrWhiteSpace(dto.RejectionReason))
                throw new ApplicationException("Rejection reason is required when rejecting a request.");

            // Get the request details
            var request = await _builderRepo.GetFlatOwnerRequestByIdAsync(dto.RequestId);
            if (request == null)
                throw new ApplicationException("Flat owner request not found.");

            if (request.Status != "Pending")
                throw new ApplicationException($"Request is already {request.Status}.");

            // Update the request status
            var updated = await _builderRepo.UpdateFlatOwnerRequestStatusAsync(
                dto.RequestId,
                dto.Action,
                adminId,
                dto.RejectionReason
            );

            if (!updated)
                throw new ApplicationException("Failed to update request status.");

            // If approved, create the flat owner
            if (dto.Action == "Approved")
            {
                var createModel = new CreateFlatOwnerModel
                {
                    PropertyId = request.PropertyId,
                    BuildingId = request.BuildingId,
                    FlatNo = request.FlatNumber,
                    UserId = request.UserId,
                    RoleId = request.RoleId,
                    GuestType = request.GuestType,
                    CreatedBy = adminId // Admin is creating it now
                };

                await _builderRepo.CreateFlatOwnerRepoAsync(createModel);

                // Create a permanent QR for this flat owner (mirror the original behavior)
                try
                {
                    var token = QrHelper.GenerateToken();

                    var visitorPayload = new
                    {
                        visitorname = "Self",
                        phone = "",
                        purpose = "Permanent QR for flat owner",
                        propertyid = createModel.PropertyId,
                        buildingid = createModel.BuildingId,
                        flatid = createModel.FlatNo,
                        userid = createModel.UserId,
                        qrcode = token,
                        qr_type = "unlimited",
                        flatownerid = createModel.UserId
                    };

                    await _flatownerRepo.CreateVisitorRequestAsync(visitorPayload);
                }
                catch (Exception ex)
                {
                    throw new ApplicationException("Flat owner created but failed to create permanent QR: " + ex.Message);
                }
            }

            return true;
        }

        public async Task<IEnumerable<FlatOwnerRequestResponseModel>> GetAllFlatOwnerRequestsAsync(string? status = null)
        {
            return await _builderRepo.GetAllFlatOwnerRequestsAsync(status);
        }
    }
}
