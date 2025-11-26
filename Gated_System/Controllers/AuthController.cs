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
                return Ok(new
                {
                    message = "User registered Successfully",
                    data = res
                });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel dto)
        {
            var res = await _auth.LoginAsync(dto);
            if (res == null) return Unauthorized(new { message = "Invalid credentials" });
            return Ok(new
            {
                message = "User Login Successfully",
                data = res
            });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] string refreshToken)
        {
            var res = await _auth.RefreshAsync(refreshToken);
            if (res == null) return Unauthorized();
            return Ok(res);
        }

        [Authorize]
        [HttpPost("registerproperty")]
        public async Task<IActionResult> Create([FromBody] PropertyCreateModel dto)
        {
            try
            {
                var id = await _auth.CreatePropertyAsync(dto);

                return Ok(new
                {
                    message = "Property created successfully",
                    data = new
                    {
                        id,
                        propertyname = dto.PropertyName,
                        address = dto.Address,
                        city = dto.City,
                        pincode = dto.Pincode,
                        builderid = dto.BuilderId,
                        buildingCount = dto.Buildings?.Count ?? 0,
                        buildings = dto.Buildings
                    }
                });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                // log ex if you have logger
                return StatusCode(500, new { message = "Internal server error", detail = ex.Message });
            }
        }
    }
}
