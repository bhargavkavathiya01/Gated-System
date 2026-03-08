using Gated_System.Helpers;
using Gated_System.Models;
using Gated_System.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Gated_System.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SecurityController : ControllerBase
    {
        private readonly ISecurityService _service;
        public SecurityController(ISecurityService service) => _service = service;

        private int GetCurrentUserId()
        {
            var idClaim = User.FindFirst("userId")?.Value;

            if (string.IsNullOrEmpty(idClaim))
                return -1;

            if (!int.TryParse(idClaim, out var uid))
                return -1;

            return uid;
        }

        [HttpPost("verify")]
        public async Task<IActionResult> VerifyQr([FromBody] VerifyQrRequest req)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == -1)
                    return Unauthorized(ApiResponse.Fail("Invalid or expired token."));

                var result = await _service.VerifyQrAsync(req, userId);
                //return Ok(new { status = true, message = "Entry allowed", data = result });
                return Ok(ApiResponse.Success("Entry allowed",result));
            }
            catch (ApplicationException ex)
            {
                if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                    //return NotFound(new { status = false, message = ex.Message , data = new { } });
                    return NotFound(ApiResponse.Fail(ex.Message));
                if (ex.Message.Contains("expired", StringComparison.OrdinalIgnoreCase))
                    //return StatusCode(410, new { status = false, message = ex.Message , data = new { } });
                    return StatusCode(410, ApiResponse.Fail(ex.Message));
                if (ex.Message.Contains("rejected", StringComparison.OrdinalIgnoreCase))
                    return Forbid();

                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status= false, message = "An error occurred", details = ex.Message });
            }
        }

        [HttpPost("manual-entry")]
        public async Task<IActionResult> ManualEntry([FromBody] ManualEntryRequest req)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == -1)
                    return Unauthorized(ApiResponse.Fail("Invalid or expired token."));

                // Call service
                var id = await _service.CreateManualVisitorAsync(req, userId);
                
                return Ok(ApiResponse.Success("Visitor request created successfully", new { id }));
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

        [HttpPost("emergency-entry")]
        public async Task<IActionResult> EmergencyEntry([FromBody] EmergencyEntryRequest req)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == -1)
                    return Unauthorized(ApiResponse.Fail("Invalid or expired token."));

                // Call service
                var id = await _service.CreateEmergencyEntryAsync(req, userId);
                
                return Ok(ApiResponse.Success("Emergency entry recorded successfully", new { id }));
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

        //[HttpPost("checkout")]
        //public async Task<IActionResult> Checkout([FromBody] CheckoutRequest req)
        //{
        //    try
        //    {
        //        await _service.CheckoutAsync(req);
        //        return Ok(new { status = true, message = "Checked out" });
        //    }
        //    catch (ApplicationException ex)
        //    {
        //        return BadRequest(new { message = ex.Message });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { message = "An error occurred", details = ex.Message });
        //    }
        //}
    }
}
