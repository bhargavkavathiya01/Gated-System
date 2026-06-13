using Gated_System.Helpers;
using Gated_System.Models;
using Gated_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Gated_System.Controllers
{
    //[Authorize(Roles = "Builder")]
    [Route("api/[controller]")]
    [ApiController]
    public class BuilderController : ControllerBase
    {
        private readonly IBuilderService _service;
        private readonly IAwsS3Service _aws;

        public BuilderController(IBuilderService service, IAwsS3Service aws)
        {
            _service = service;
            _aws = aws;
        }

        private int GetCurrentUserId()
        {
            var idClaim = User?.FindFirst("userId")?.Value;

            if (string.IsNullOrEmpty(idClaim))
                return -1;

            if (!int.TryParse(idClaim, out var uid))
                return -1;

            return uid;
        }

        [HttpGet("getroles")]
        public async Task<IActionResult> GetRoles()
        {
            var roles = await _service.GetRolesAsync();

            return Ok(new
            {
                status = true,
                message = "Roles fetched successfully",
                data = new { roles }
            });
        }

        [HttpPost("createsecretaryorsecurity")]
        public async Task<IActionResult> CreateSecretary([FromBody] CreateSecretaryModel dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == -1)
                    return Unauthorized(ApiResponse.Fail("Invalid or expired token."));
                dto.CreatedBy = userId;

                var resultId = await _service.CreateSecretaryAsync(dto);
                return Ok(new
                {
                    status = true,
                    message = "Secretary created successfully",
                    data = new { id = resultId }
                });
            }
            catch (ApplicationException ex)
            {
                return Conflict(new
                {
                    status = false,
                    message = ex.Message,
                    data = new {}
                });
            }
            catch (Exception ex)
            {
                // consider logging ex
                return StatusCode(500, new { message = "An error occurred", details = ex.Message });
            }
        }

        [HttpPost("createflatowner")]
        public async Task<IActionResult> CreateFlatOwner([FromForm] CreateFlatOwnerModel dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == -1)
                    return Unauthorized(ApiResponse.Fail("Invalid or expired token."));

                if (dto.AadharCard != null)
                {
                    var res = await _aws.UploadFileAsync(dto.AadharCard, "AadharCards");
                    if (res.status) dto.AadharCardUrl = res.Data;
                }
                if (dto.ElectricityBill != null)
                {
                    var res = await _aws.UploadFileAsync(dto.ElectricityBill, "ElectricityBills");
                    if (res.status) dto.ElectricityBillUrl = res.Data;
                }

                // Convert to request model
                var requestModel = new CreateFlatOwnerRequestModel
                {
                    PropertyId = dto.PropertyId,
                    BuildingId = dto.BuildingId,
                    FlatNo = dto.FlatNo,
                    UserId = dto.UserId,
                    RoleId = dto.RoleId,
                    GuestType = dto.GuestType,
                    RequestedBy = userId,
                    AadharCardUrl = dto.AadharCardUrl,
                    ElectricityBillUrl = dto.ElectricityBillUrl
                };

                var resultId = await _service.CreateFlatOwnerRequestAsync(requestModel);
                return Ok(new
                {
                    status = true,
                    message = "Flat Owner creation request submitted successfully. Waiting for admin approval.",
                    data = new { id = resultId }
                });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                // consider logging ex
                return StatusCode(500, new { message = "An error occurred", details = ex.Message });
            }
        }

        [HttpGet("flatownerrequests")]
        public async Task<IActionResult> GetFlatOwnerRequests([FromQuery] string? status = null)
        {
            try
            {
                var requests = await _service.GetAllFlatOwnerRequestsAsync(status);
                return Ok(new
                {
                    status = true,
                    message = "Flat owner requests fetched successfully",
                    data = requests
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", details = ex.Message });
            }
        }

        [HttpGet("propertiesbybuilder")]
        public async Task<IActionResult> GetPropertiesByBuilder()
        {
            try
            {
                var builderId = GetCurrentUserId();
                if (builderId == -1)
                    return Unauthorized(ApiResponse.Fail("Invalid or expired token."));

                var properties = await _service.GetPropertiesByBuilderIdAsync(builderId);

                return Ok(new
                {
                    status = true,
                    message = properties != null && properties.Any()
                        ? "Properties fetched successfully"
                        : "No properties found for given builder",
                    data = properties
                });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                // consider logging ex
                return StatusCode(500, new { message = "An error occurred", details = ex.Message });
            }
        }

        [HttpGet("getallusers")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _service.GetAllUsersAsync();

            return Ok(new
            {
                status = true,
                message = "Users fetched successfully",
                data = users
            });
        }

        [HttpPost("getuserbyemailorphone")]
        public async Task<IActionResult> GetUserByEmailOrPhone([FromBody] EmailOrPhoneModel model)
        {
            if (string.IsNullOrWhiteSpace(model.user))
                return BadRequest(ApiResponse.Fail("Email or phone is required"));

            var result = await _service.GetUserByEmailOrPhoneAsync(model.user);

            if (result == null)
                return NotFound(ApiResponse.Fail("User not found"));

            return Ok(ApiResponse.Success("User fetched successfully", result));
        }

        [HttpGet("memberdetails/{propertyId}")]
        public async Task<IActionResult> GetMemberDetails(int propertyId)
        {
            // Encapsulate ID into model as per your requirement
            var request = new PropertyMemberRequest { PropertyId = propertyId };

            var result = await _service.GetMemberDetailsAsync(request);

            if (!result.status)
            {
                return BadRequest(new
                {
                    status = false,
                    message = result.Message
                });
            }

            return Ok(new
            {
                status = true,
                message = result.Message,
                data = result.Data
            });
        }
    }
}
