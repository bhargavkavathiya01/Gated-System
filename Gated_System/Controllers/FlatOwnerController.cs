using Gated_System.Models;
using Gated_System.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Gated_System.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FlatOwnerController : ControllerBase
    {
        private readonly IFlatOwnerService _service;

        public FlatOwnerController(IFlatOwnerService service) => _service = service;

        [HttpPost("invitevisitor")]
        public async Task<IActionResult> CreateVisitor([FromBody] CreateVisitorDto dto)
        {
            try
            {
                var result = await _service.CreateVisitorAsync(dto);
                return Ok(new
                {
                    status = true,
                    message = "Visitor created successfully",
                    data = new
                    {
                        id = result.Id,
                        qrcode = result.QrToken,
                        qrImageBase64 = result.QrImageBase64,
                        expiry = result.ExpiryUtc
                    }
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

        [HttpGet("visitor/{id:int}")]
        public async Task<IActionResult> GetVisitorById(int id)
        {
            try
            {
                var dto = await _service.GetVisitorByIdAsync(id);
                if (dto == null) return NotFound(new { message = "Visitor not found" });

                return Ok(new { status = true, message = "Visitor fetched", data = dto });
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

        [HttpPost("createpg")]
        public async Task<IActionResult> CreatePGMembers([FromBody] CreateFlatOwnerModel dto)
        {
            try
            {
                var resultId = await _service.CreatePGMembers(dto);
                return Ok(new
                {
                    status = true,
                    message = "PG created successfully",
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
