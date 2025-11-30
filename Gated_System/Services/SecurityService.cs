using Gated_System.Models;
using Gated_System.Repositories;
using System.Text.Json;

namespace Gated_System.Services
{
    public class SecurityService : ISecurityService
    {
        private readonly ISecurityRepository _repo;
        public SecurityService(ISecurityRepository repo) => _repo = repo;

        public async Task<object> VerifyQrAsync(VerifyQrRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.QrToken))
                throw new ApplicationException("QrToken required.");

            // 1) verify QR
            var verifyPayload = new { qrcode = req.QrToken };
            using var doc = await _repo.VerifyQrRawAsync(verifyPayload);
            var root = doc.RootElement;
            var status = root.GetProperty("status_code").GetInt32();

            if (status == 404) throw new ApplicationException("QR not found");
            if (status == 410) throw new ApplicationException("QR expired");
            if (status == 403)
            {
                // could be 'Rejected' or 'already used' depending on SP payload
                var msg = root.TryGetProperty("message", out var m) ? m.GetString() ?? "QR not allowed" : "QR not allowed";
                throw new ApplicationException(msg);
            }

            if (status != 200)
            {
                var msg = root.TryGetProperty("message", out var m) ? m.GetString() ?? "Error verifying QR" : "Error verifying QR";
                throw new ApplicationException(msg);
            }

            var item = root.GetProperty("data").EnumerateArray().First();
            var visitorRequestId = item.GetProperty("id").GetInt32();

            // 2) create visitor log
            var logPayload = new
            {
                visitorrequestid = visitorRequestId,
                securityid = req.SecurityId,
                entrytime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                remarks = (string?)null
            };

            using var logDoc = await _repo.CreateVisitorLogRawAsync(logPayload);
            var logRoot = logDoc.RootElement;
            var logStatus = logRoot.GetProperty("status_code").GetInt32();
            if (logStatus != 201)
            {
                var msg = logRoot.TryGetProperty("message", out var m) ? m.GetString() ?? "Visitor log failed" : "Visitor log failed";
                throw new ApplicationException(msg);
            }

            // 3) update visitor request status (optional)
            var updatePayload = new
            {
                id = visitorRequestId,
                status = "Approved",
                modifiedby = req.SecurityId
            };
            using var updDoc = await _repo.UpdateVisitorRequestStatusRawAsync(updatePayload);
            var updRoot = updDoc.RootElement;
            var updStatus = updRoot.GetProperty("status_code").GetInt32();
            //if (updStatus < 200 || updStatus >= 300) throw new ApplicationException("Failed updating visitor request status.");
            if (updStatus < 200 || updStatus >= 300)
            {
                var msg = updRoot.TryGetProperty("message", out var m) ? m.GetString() ?? "Failed updating visitor request status" : "Failed updating visitor request status";
                throw new ApplicationException(msg);
            }

            var visitorLogJson = logRoot.GetProperty("data").GetRawText();
            var visitorLog = JsonSerializer.Deserialize<object>(visitorLogJson);

            return new
            {
                visitorRequestId,
                visitorLog
            };
        }

        public async Task CheckoutAsync(CheckoutRequest req)
        {
            if (req == null) throw new ApplicationException("Invalid request.");

            var payload = new
            {
                id = req.VisitorLogId,
                exittime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                remarks = req.Remarks
            };

            using var doc = await _repo.UpdateVisitorLogExitRawAsync(payload);
            var root = doc.RootElement;
            var status = root.GetProperty("status_code").GetInt32();
            if (status != 200) throw new ApplicationException("Checkout failed.");
        }
    }
}
