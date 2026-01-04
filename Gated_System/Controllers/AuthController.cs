using Gated_System.Helpers;
using Gated_System.Models;
using Gated_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Gated_System.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;

        public AuthController(IAuthService auth) => _auth = auth;

        private int GetCurrentUserId()
        {
            var idClaim = User.FindFirst("userId")?.Value;

            if (string.IsNullOrEmpty(idClaim))
                return -1;

            if (!int.TryParse(idClaim, out var uid))
                return -1;

            return uid;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterModel dto)
        {
            try
            {
                var res = await _auth.RegisterAsync(dto);
                //return Ok(ApiResponse.Success("User registered successfully", res));
                return Ok(new
                {
                    status = true,
                    message = "User registered Successfully",
                    data = res
                });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel dto)
        {
            try
            {
                var res = await _auth.LoginAsync(dto);
                if (!res.status) return Unauthorized(ApiResponse.Fail(res.Message));
                return Ok(res);
            }
            catch (ApplicationException ex)
            {
                // This will catch "Email already exists" or "Phone Number already exists"
                return Conflict(new { status = false, message = ex.Message });
            }
        }

        [HttpGet("getuserbytoken")]
        public async Task<IActionResult> GetUserDataByToken()
        {
            var userId = GetCurrentUserId();
            if (userId == -1)
                return Unauthorized(ApiResponse.Fail("Invalid or expired token."));

            var res = await _auth.GetUserByToken(userId);
            if (res == null) return Unauthorized(ApiResponse.Fail("Invalid or expired token."));
            return Ok(ApiResponse.Success("User Fetched successful", res));
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenModel dto)
        {
            var res = await _auth.RefreshAsync(dto.RefreshToken);
            if (res == null)
                return Unauthorized(ApiResponse.Fail("Invalid refresh token"));
            return Ok(ApiResponse.Success("Token refreshed successfully", res));
        }

        [Authorize]
        [HttpPost("registerproperty")]
        public async Task<IActionResult> Create([FromBody] PropertyCreateModel dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == -1)
                    return Unauthorized(ApiResponse.Fail("Invalid or expired token."));
                dto.BuilderId = userId;
                var id = await _auth.CreatePropertyAsync(dto);
                var returnData = new
                {
                    id,
                    propertyname = dto.PropertyName,
                    address = dto.Address,
                    city = dto.City,
                    pincode = dto.Pincode,
                    builderid = dto.BuilderId,
                    buildingCount = dto.Buildings?.Count ?? 0,
                    buildings = dto.Buildings
                };
                return Ok(ApiResponse.Success("Property created successfully", returnData));
            }
            catch (ApplicationException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                // log ex if you have logger
                return StatusCode(500, ApiResponse.Fail("Internal server error", new { detail = ex.Message }));
            }
        }

        [HttpPost("forgotpassword")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (string.IsNullOrEmpty(request.Email))
                return BadRequest(new { status = false, message = "Email is required" });

            var result = await _auth.ForgotPasswordAsync(request);

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
    }
}
