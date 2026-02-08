using Gated_System.Helpers;
using Gated_System.Models;
using Gated_System.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static Gated_System.Models.QRModel;

namespace Gated_System.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FlatOwnerController : ControllerBase
    {
        private readonly IFlatOwnerService _service;

        public FlatOwnerController(IFlatOwnerService service) => _service = service;

        private int GetCurrentUserId()
        {
            var idClaim = User?.FindFirst("userId")?.Value;

            if (string.IsNullOrEmpty(idClaim))
                return -1;

            if (!int.TryParse(idClaim, out var uid))
                return -1;

            return uid;
        }

        [HttpPost("invitevisitor")]
        public async Task<IActionResult> CreateVisitor([FromBody] CreateVisitorDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == -1)
                    return Unauthorized(ApiResponse.Fail("Invalid or expired token."));
                dto.RequestedBy = userId;

                var result = await _service.CreateVisitorAsync(dto);
                //return Ok(new
                //{
                //    status = true,
                //    message = "Visitor created successfully",
                //    data = new
                //    {
                //        id = result.Id,
                //        qrcode = result.QrToken,
                //        //qrImageBase64 = result.QrImageBase64,
                //        expiry = result.ExpiryUtc
                //    }
                //});
                return Ok(ApiResponse.Success("Visitor created successfully", new
                {
                    id = result.Id,
                    qrcode = result.QrToken,
                    //qrImageBase64 = result.QrImageBase64,
                    expiry = result.ExpiryUtc
                }));
            }
            catch (ApplicationException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }

        [HttpGet("visitor/{id:int}")]
        public async Task<IActionResult> GetVisitorById(int id)
        {
            try
            {
                var dto = await _service.GetVisitorByIdAsync(id);
                if (dto == null) return NotFound(new { message = "Visitor not found" });

                //return Ok(new { status = true, message = "Visitor fetched", data = dto });
                return Ok(ApiResponse.Success("Visitor fetched",dto));
            }
            catch (ApplicationException ex)
            {
                //return BadRequest(new { message = ex.Message });
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                //return StatusCode(500, new { message = "An error occurred", details = ex.Message });
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }

        [HttpPost("createpg")]
        public async Task<IActionResult> CreatePGMembers([FromBody] CreateFlatOwnerModel dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == -1)
                    return Unauthorized(ApiResponse.Fail("Invalid or expired token."));
                dto.CreatedBy = userId;
                var resultId = await _service.CreatePGMembers(dto);
                return Ok(ApiResponse.Success("PG created successfully", new { id = resultId }));
                //return Ok(new
                //{
                //    status = true,
                //    message = "PG created successfully",
                //    data = new { id = resultId }
                //});
            }
            catch (ApplicationException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                // consider logging ex
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }

        [HttpPost("getpgs")]
        public async Task<IActionResult> GetPGs([FromBody] PGMemberRequest request)
        {
            var result = await _service.GetPGMembersAsync(request);
            return Ok(ApiResponse.Success(result.Message,result.Data));
            //return Ok(new { status = true, message = result.Message, data = result.Data });
        }

        [HttpDelete("deletepg")]
        public async Task<IActionResult> DeletePG([FromBody] DeletePGRequest request)
        {
            var result = await _service.DeletePGMemberAsync(request);

            if (!result.status)
                return BadRequest(ApiResponse.Fail(result.Message));
                //return BadRequest(new { status = false, message = result.Message });

            return Ok(ApiResponse.Success(result.Message));
            //return Ok(new { status = true, message = result.Message });
        }

        [HttpGet("getqrhistory")]
        public async Task<IActionResult> GetHistory()
        {
            var userId = GetCurrentUserId();
            if (userId == -1)
                return Unauthorized(ApiResponse.Fail("Invalid or expired token."));

            var request = new QRHistoryRequest { UserId = userId };
            var result = await _service.GetHistoryAsync(request);
            return Ok(new { status = true, message = result.Message, data = result.Data });
        }

        [HttpPost("revokeqr")]
        public async Task<IActionResult> RevokeQR([FromBody] RevokeQRRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == -1)
                return Unauthorized(ApiResponse.Fail("Invalid or expired token."));
            request.UserId= userId;

            var result = await _service.RevokeAsync(request);
            if (!result.status) return BadRequest(new { status = false, message = result.Message });

            return Ok(new { status = true, message = result.Message });
        }

        [HttpPost("registerToken")]
        public async Task<IActionResult> RegisterToken([FromBody] UserDeviceTokenModel dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == -1)
                    return Unauthorized(ApiResponse.Fail("Invalid or expired token."));

                // Attach current user id from token to the DTO
                dto.UserId = userId;

                await _service.RegisterDeviceTokenAsync(dto);

                return Ok(ApiResponse.Success("Device token registered successfully", null));
            }
            catch (ApplicationException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Fail("Internal Server Error: " + ex.Message));
            }
        }

        [HttpPost("approveorrejectmanualvisitor")]
        public async Task<IActionResult> ApproveVisitor([FromBody] VisitorApprovalRequest req)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == -1)
                    return Unauthorized(ApiResponse.Fail("Invalid or expired token."));

                req.ApprovedBy = userId; // Enforce logged-in user

                await _service.ApproveVisitorRequestAsync(req);
                return Ok(ApiResponse.Success($"Visitor request {req.Status.ToLower()} successfully."));
            }
            catch (ApplicationException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }
    }
}
