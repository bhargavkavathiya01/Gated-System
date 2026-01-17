using Gated_System.Helpers;
using Gated_System.Models;
using Gated_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Gated_System.Controllers
{
    //[Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class SuperAdminController : ControllerBase
    {
        private readonly ISuperAdminService _service;

        public SuperAdminController(ISuperAdminService service) => _service = service;

        private int GetCurrentUserId()
        {
            var idClaim = User?.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(idClaim))
                return -1;

            if (!int.TryParse(idClaim, out var uid))
                return -1;

            return uid;
        }

        [HttpGet("getallproperties")]
        public async Task<IActionResult> GetAllProperties()
        {
            try
            {
                var list = await _service.GetAllPropertiesAsync();
                return Ok(new
                {
                    status = true,
                    message = list.Any() ? "Properties fetched successfully" : "No properties found",
                    data = list
                });
            }
            catch (ApplicationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = "An error occurred", details = ex.Message }); }
        }

        [HttpGet("getpendingproperties")]
        public async Task<IActionResult> GetAllPropertiesByStatus()
        {
            try
            {
                var list = await _service.GetAllPropertiesByStatusAsync("Pending");
                return Ok(new
                {
                    status = true,
                    message = list.Any() ? $"Properties with status pending fetched" : $"No properties with status Pending found",
                    data = list
                });
            }
            catch (ApplicationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = "An error occurred", details = ex.Message }); }
        }

        [HttpPost("verifyproperty")]
        public async Task<IActionResult> UpdateVerification([FromBody] UpdatePropertyVerificationModel dto)
        {
            try
            {
                if (dto.PropertyId <= 0) return BadRequest(new { message = "Invalid property id." });
                if (dto == null) return BadRequest(new { message = "Body is required." });
                if (string.IsNullOrWhiteSpace(dto.IsVerified)) return BadRequest(new { message = "IsVerified is required." });


                string resultMessage = await _service.UpdatePropertyVerificationAsync(dto);

                return Ok(new
                {
                    status = true,
                    message = resultMessage
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
                    message = requests.Any() ? "Flat owner requests fetched successfully" : "No requests found",
                    data = requests
                });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", details = ex.Message });
            }
        }

        [HttpPost("approveflatownerrequest")]
        public async Task<IActionResult> ApproveFlatOwnerRequest([FromBody] ApproveFlatOwnerRequestModel dto)
        {
            try
            {
                var adminId = GetCurrentUserId();
                if (adminId == -1)
                    return Unauthorized(ApiResponse.Fail("Invalid or expired token."));

                var result = await _service.ApproveFlatOwnerRequestAsync(dto, adminId);

                return Ok(new
                {
                    status = true,
                    message = $"Flat owner request {dto.Action.ToLower()} successfully",
                    data = new { success = result }
                });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", details = ex.Message });
            }
        }
    }
}
