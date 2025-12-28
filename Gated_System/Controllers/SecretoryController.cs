using Gated_System.Helpers;
using Gated_System.Models;
using Gated_System.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Gated_System.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SecretoryController : ControllerBase
    {
        private readonly ISecretoryService _service;

        public SecretoryController(ISecretoryService service) => _service = service;

        private int GetCurrentUserId()
        {
            var idClaim = User?.FindFirst("userId")?.Value;

            if (string.IsNullOrEmpty(idClaim))
                return -1;

            if (!int.TryParse(idClaim, out var uid))
                return -1;

            return uid;
        }


        [HttpPost("createcommitteemember")]
        public async Task<IActionResult> CreateCommitteeMember([FromBody] CreateCommitteeModel dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == -1)
                    return Unauthorized(ApiResponse.Fail("Invalid or expired token."));
                dto.CreatedBy = userId;

                var resultId = await _service.CreateCommitteeAsync(dto);
                return Ok(new
                {
                    status = true,
                    message = "Committee Member Created Successfully",
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

    }
}
