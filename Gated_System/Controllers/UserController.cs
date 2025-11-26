using Gated_System.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Gated_System.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserRepository _repo;

        public UserController(IUserRepository repo) => _repo = repo;

        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            if (!HttpContext.Items.TryGetValue("UserId", out var uidObj) || uidObj is not int uid)
                return Unauthorized();

            var user = await _repo.GetByIdAsync(uid);
            if (user == null) return NotFound();

            return Ok(new { user.Id, user.Firstname, user.Email, user.Phone });
        }
    }
}
