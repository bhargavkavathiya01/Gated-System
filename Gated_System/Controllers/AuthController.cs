using Gated_System.Helpers;
using Gated_System.Models;
using Gated_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Gated_System.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;

        public AuthController(IAuthService auth) => _auth = auth;

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterModel dto)
        {
            try
            {
                var res = await _auth.RegisterAsync(dto);
                return Ok(ApiResponse.Success("User registered successfully", res));
                //return Ok(new
                //{
                //    message = "User registered Successfully",
                //    data = res
                //});
            }
            catch (ApplicationException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel dto)
        {
            var res = await _auth.LoginAsync(dto);
            if (res == null) return Unauthorized(ApiResponse.Fail("Invalid credentials"));
            return Ok(ApiResponse.Success("User login successful", res));
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
    }
}
