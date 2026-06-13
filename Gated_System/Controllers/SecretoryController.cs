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
        private readonly IAwsS3Service _aws;

        public SecretoryController(ISecretoryService service, IAwsS3Service aws)
        {
            _service = service;
            _aws = aws;
        }

        private int GetCurrentUserId()
        {
            var idClaim = User?.FindFirst("userId")?.Value;

            if (string.IsNullOrEmpty(idClaim))
                return -1;

            if (!int.TryParse(idClaim, out var uid))
                return -1;

            return uid;
        }


        [HttpPost("uploadsocietyimages")]
        public async Task<IActionResult> UploadSocietyImages([FromForm] SocietyImageUploadModel dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == -1)
                    return Unauthorized(ApiResponse.Fail("Invalid or expired token."));

                if (dto.Images == null || dto.Images.Count == 0)
                    return BadRequest(ApiResponse.Fail("At least one image is required."));

                dto.UploadedBy = userId;

                var uploadedUrls = new List<string>();
                foreach (var file in dto.Images)
                {
                    var res = await _aws.UploadFileAsync(file, "SocietyImages");
                    if (!res.status)
                        return BadRequest(ApiResponse.Fail($"Failed to upload {file.FileName}: {res.Message}"));
                    uploadedUrls.Add(res.Data);
                }

                var ids = await _service.UploadSocietyImagesAsync(dto, uploadedUrls);
                return Ok(ApiResponse.Success("Society images uploaded successfully", new { count = ids.Count, ids }));
            }
            catch (ApplicationException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }

        [HttpGet("getsocietyimages")]
        public async Task<IActionResult> GetSocietyImages([FromQuery] int propertyId)
        {
            try
            {
                if (propertyId <= 0)
                    return BadRequest(ApiResponse.Fail("Invalid propertyId."));

                var images = await _service.GetSocietyImagesAsync(propertyId);
                return Ok(ApiResponse.Success("Society images fetched successfully", images));
            }
            catch (ApplicationException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
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
