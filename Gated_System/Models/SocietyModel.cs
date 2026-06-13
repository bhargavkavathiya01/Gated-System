using Microsoft.AspNetCore.Http;

namespace Gated_System.Models
{
    public class SocietyImageUploadModel
    {
        public int PropertyId { get; set; }
        public string? ImageTitle { get; set; }
        public List<IFormFile> Images { get; set; } = new();
        public int UploadedBy { get; set; }
    }

    public class SocietyImageResponseModel
    {
        public int Id { get; set; }
        public int PropertyId { get; set; }
        public string ImageUrl { get; set; } = "";
        public string? ImageTitle { get; set; }
        public int UploadedBy { get; set; }
        public string UploadedByName { get; set; } = "";
        public DateTime CreatedOn { get; set; }
    }
}
