using Gated_System.Helpers;
using Gated_System.Models;
using Gated_System.Repositories;
using System.Text.Json;

namespace Gated_System.Services
{
    public class SecurityService : ISecurityService
    {
        private readonly ISecurityRepository _repo;
        private readonly IFlatOwnerRepository _flatOwnerRepo;
        private readonly IUserRepository _userRepo;
        private readonly PushNotificationHelper _pushHelper;

        public SecurityService(
            ISecurityRepository repo,
            IFlatOwnerRepository flatOwnerRepo,
            IUserRepository userRepo,
            PushNotificationHelper pushHelper)
        {
            _repo = repo;
            _flatOwnerRepo = flatOwnerRepo;
            _userRepo = userRepo;
            _pushHelper = pushHelper;
        }

        public async Task<object> VerifyQrAsync(VerifyQrRequest req,int SecurityId)
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

            int usedCount = item.TryGetProperty("used_count", out var uc) ? uc.GetInt32() : 0;
            int? maxUses = item.TryGetProperty("max_uses", out var mu) && mu.ValueKind != JsonValueKind.Null ? mu.GetInt32() : null;
            DateTime? expiry = item.TryGetProperty("expiry", out var ex) && ex.ValueKind != JsonValueKind.Null ? ex.GetDateTime() : null;

            var qrPropertyId = item.GetProperty("propertyid").GetInt32();

            if (qrPropertyId != req.PropertyId)
            {
                throw new ApplicationException("Invalid QR for this society.");
            }

            string nextStatus = "Active";

            if (expiry.HasValue && expiry.Value < DateTime.Now)
            {
                nextStatus = "Expired";
            }
            // If this is the last allowed usage (e.g. max is 2, current is 1, so this scan makes it 2)
            else if (maxUses.HasValue && (usedCount + 1) >= maxUses.Value)
            {
                nextStatus = "Expired";
            }

            // 2) create visitor log
            var logPayload = new
            {
                visitorrequestid = visitorRequestId,
                securityid = SecurityId,
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
                //status = "Approved",
                status = nextStatus,
                modifiedby = SecurityId
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

        public async Task<int> CreateManualVisitorAsync(ManualEntryRequest req, int securityId)
        {
            if (req == null) throw new ApplicationException("Request cannot be null.");
            if (string.IsNullOrWhiteSpace(req.VisitorName)) throw new ApplicationException("Visitor name is required.");
            if (req.PropertyId <= 0) throw new ApplicationException("PropertyId is required.");
            if (req.BuildingId <= 0) throw new ApplicationException("BuildingId is required.");
            if (req.FlatId == null) throw new ApplicationException("FlatId is required.");

            // 1. Create Visitor Request
            // Generate a manual QR code string
            var manualQr = "MANUAL-" + Guid.NewGuid().ToString("N")[..10].ToUpper(); 

            var payload = new
            {
                visitorname = req.VisitorName,
                phone = req.Phone,
                purpose = req.Purpose,
                propertyid = req.PropertyId,
                buildingid = req.BuildingId,
                flatid = req.FlatId,
                requestedby = securityId,
                qrcode = manualQr,
                expiry = DateTime.UtcNow.AddMinutes(30).ToString("o"), // Short expiry for approval
                qr_type = "manual",
                max_uses = 1,
                status = "Active"
            };

            int insertedId = await _repo.CreateManualVisitorRequestAsync(payload);

            // 2. Send Notification to specific Flat Owner (Directly)
            if (req.FlatOwnerId > 0)
            {
                var deviceToken = await _userRepo.GetDeviceTokenAsync(req.FlatOwnerId);
                if (!string.IsNullOrEmpty(deviceToken))
                {
                    await _pushHelper.SendToDeviceAsync(
                        deviceToken,
                        "Visitor Approval Request",
                        $"Visitor {req.VisitorName} is waiting for your approval.",
                        new Dictionary<string, string>
                        {
                            { "visitorRequestId", insertedId.ToString() },
                            { "action", "approve_reject" },
                            { "type", "visitor_request" }
                        }
                    );
                }
            }

            return insertedId;
        }    }
}
