using Gated_System.Models;
using Gated_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Gated_System.Controllers
{
    [Authorize(Roles = "Builder")]
    [Route("api/[controller]")]
    [ApiController]
    public class BuilderController : ControllerBase
    {
        private readonly IBuilderService _service;

        public BuilderController(IBuilderService service) => _service = service;

        [HttpPost("createsecretary")]
        public async Task<IActionResult> CreateSecretary([FromBody] CreateSecretaryModel dto)
        {
            try
            {
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
