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
                return Ok(new { status = true, message = "Entry allowed", data = result });
            }
            catch (ApplicationException ex)
            {
                if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                    return NotFound(new { status = false, message = ex.Message });
                if (ex.Message.Contains("expired", StringComparison.OrdinalIgnoreCase))
                    return StatusCode(410, new { status = false, message = ex.Message });
                if (ex.Message.Contains("rejected", StringComparison.OrdinalIgnoreCase))
                    return Forbid();

                return BadRequest(new {status=false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status= false, message = "An error occurred", details = ex.Message });
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
