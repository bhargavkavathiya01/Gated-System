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

        [HttpPost("verify")]
        public async Task<IActionResult> VerifyQr([FromBody] VerifyQrRequest req)
        {
            try
            {
                var result = await _service.VerifyQrAsync(req);
                return Ok(new { status = true, message = "Entry allowed", data = result });
            }
            catch (ApplicationException ex)
            {
                if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                    return NotFound(new { message = ex.Message });
                if (ex.Message.Contains("expired", StringComparison.OrdinalIgnoreCase))
                    return StatusCode(410, new { message = ex.Message });
                if (ex.Message.Contains("rejected", StringComparison.OrdinalIgnoreCase))
                    return Forbid();

                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", details = ex.Message });
            }
        }

        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout([FromBody] CheckoutRequest req)
        {
            try
            {
                await _service.CheckoutAsync(req);
                return Ok(new { status = true, message = "Checked out" });
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
