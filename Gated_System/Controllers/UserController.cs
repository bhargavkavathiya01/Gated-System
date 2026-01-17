using Gated_System.Models;
using Gated_System.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Gated_System.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        private int GetCurrentUserId()
        {
            var idClaim = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(idClaim) || !int.TryParse(idClaim, out var uid))
                return -1;
            return uid;
        }

        [HttpPut("update-profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UserModel dto)
        {
            int currentUserId = GetCurrentUserId();
            if (currentUserId == -1) return Unauthorized();

            // Safety: Ensure users can only update their own profile 
            // OR the ID is set from the token for security
            dto.Id = currentUserId;
            dto.CreatedBy = currentUserId;

            var result = await _userService.UpdateProfileAsync(dto);

            if (!result.status)
            {
                // This handles 409 Conflict (Email/Phone exists) or 404
                if (result.Message.Contains("exists"))
                    return Conflict(new { status = false, message = result.Message , data = new { } });

                return BadRequest(new { status = false, message = result.Message , data = new { } });
            }

            return Ok(new
            {
                status = true,
                message = result.Message,
                data =new { }
            });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest requestModel)
        {
            int currentUserId = GetCurrentUserId();
            if (currentUserId == -1) return Unauthorized();

            requestModel.UserId = currentUserId;

            // Call service passing the ID and the Request Model
            var result = await _userService.ResetPasswordAsync(requestModel);

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
                message = result.Message
            });
        }

        [HttpPost("update-fcm-token")]
        public async Task<IActionResult> UpdateFcmToken([FromBody] UpdateFcmTokenModel model)
        {
            int currentUserId = GetCurrentUserId();
            if (currentUserId == -1)
                return Unauthorized(new { status = false, message = "Invalid or expired token", data = new { } });

            // Set user ID from token for security
            model.UserId = currentUserId;
            model.ModifiedBy = currentUserId;

            var result = await _userService.UpdateFcmTokenAsync(model);

            if (!result.status)
            {
                return BadRequest(new
                {
                    status = false,
                    message = result.Message,
                    data = new { }
                });
            }

            return Ok(new
            {
                status = true,
                message = result.Message,
                data = new { }
            });
        }
    }
}
