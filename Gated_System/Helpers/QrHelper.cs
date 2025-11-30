using QRCoder;

namespace Gated_System.Helpers
{
    public static class QrHelper
    {
        // generate opaque token (GUID)
        public static string GenerateToken() => Guid.NewGuid().ToString("N"); // 32 hex chars

        // generate PNG bytes (you can return base64 or FileStreamResult)
        public static byte[] GenerateQrPngBytes(string payload, int pixelsPerModule = 8)
        {
            using var qr = new QRCodeGenerator();
            using var data = qr.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
            using var code = new QRCode(data);
            using var bitmap = code.GetGraphic(pixelsPerModule);
            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return ms.ToArray();
        }

        public static string GenerateQrBase64Png(string payload, int pixelsPerModule = 8)
        {
            var bytes = GenerateQrPngBytes(payload, pixelsPerModule);
            return Convert.ToBase64String(bytes);
        }
    }
}
