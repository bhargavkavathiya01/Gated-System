using Gated_System.Helpers;
using Gated_System.Models;
using Gated_System.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using static Gated_System.Models.QRModel;

namespace Gated_System.Services
{
    public class FlatOwnerService :IFlatOwnerService
    {
        private readonly IFlatOwnerRepository _repo;
        private readonly ISecurityRepository _securityRepo;
        private readonly IUserRepository _userRepo;
        private readonly IAuthRepository _authRepo;
        private readonly PushNotificationHelper _pushHelper;

        public FlatOwnerService(
            IFlatOwnerRepository repo, 
            ISecurityRepository securityRepo,
            IUserRepository userRepo,
            IAuthRepository authRepo,
            PushNotificationHelper pushHelper)
        {
            _repo = repo;
            _securityRepo = securityRepo;
            _userRepo = userRepo;
            _authRepo = authRepo;
            _pushHelper = pushHelper;
        }

        public static readonly HashSet<string> AllowedGuestTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Tenant",
            "Family"
        };

        //public async Task<CreatedVisitorResult> CreateVisitorAsync(CreateVisitorDto dto)
        //{
        //    if (dto == null) throw new ApplicationException("Request body is required.");
        //    if (string.IsNullOrWhiteSpace(dto.VisitorName)) throw new ApplicationException("VisitorName is required.");
        //    if (dto.PropertyId <= 0) throw new ApplicationException("PropertyId is required.");

        //    // 1) generate token & expiry
        //    var token = QrHelper.GenerateToken();
        //    //var expiry = DateTime.UtcNow.AddMinutes(dto.ExpiryMinutes);
        //    DateTime? expiry = null;
        //    if (dto.QrType?.Equals("time", StringComparison.OrdinalIgnoreCase) ?? true)
        //    {
        //        expiry = DateTime.UtcNow.AddMinutes(dto.ExpiryMinutes);
        //    }

        //    int? maxUses = null;
        //    if (dto.QrType?.Equals("one_time", StringComparison.OrdinalIgnoreCase) ?? false)
        //        maxUses = dto.MaxUses ?? 1; // default 1 for one_time
        //    else
        //        maxUses = dto.MaxUses; // could be null = unlimited

        //    // 2) payload for SP
        //    var payload = new
        //    {
        //        visitorname = dto.VisitorName,
        //        phone = dto.Phone,
        //        purpose = dto.Purpose,
        //        propertyid = dto.PropertyId,
        //        buildingid = dto.BuildingId,
        //        flatid = dto.FlatId,
        //        requestedby = dto.RequestedBy,
        //        qrcode = token,
        //        expiry = expiry?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        //        qr_type = dto.QrType,
        //        max_uses = maxUses
        //    };

        //    // 3) call repo to insert
        //    var insertedId = await _repo.CreateVisitorRequestAsync(payload);

        //    // 4) generate QR PNG base64
        //    var qrBase64 = QrHelper.GenerateQrBase64Png(token);

        //    return new CreatedVisitorResult
        //    {
        //        Id = insertedId,
        //        QrToken = token,
        //        QrImageBase64 = qrBase64,
        //        ExpiryUtc = expiry ?? DateTime.UtcNow.AddYears(100)
        //    };
        //}
        public async Task<CreatedVisitorResult> CreateVisitorAsync(CreateVisitorDto dto)
        {
            if (dto == null) throw new ApplicationException("Request body is required.");
            if (string.IsNullOrWhiteSpace(dto.VisitorName)) throw new ApplicationException("VisitorName is required.");
            if (dto.PropertyId <= 0) throw new ApplicationException("PropertyId is required.");

            // normalize qr type
            var qrType = (dto.QrType ?? "time").Trim().ToLowerInvariant();

            // validate qrType
            if (qrType != "time" && qrType != "one_time" && qrType != "multi" && qrType != "unlimited")
                throw new ApplicationException("Invalid QrType. Allowed: time, one_time, multi, unlimited");

            // validate ExpiryMinutes
            if (dto.ExpiryMinutes < 0) throw new ApplicationException("ExpiryMinutes cannot be negative.");

            // 1) generate unique token
            var token = QrHelper.GenerateToken();

            // 2) compute expiry only for time-based (or when provided)
            DateTime? expiryTime = null;
            if (qrType == "time")
            {
                // default behaviour: if ExpiryMinutes==0 treat as no expiry (or you can require >0)
                if (dto.ExpiryMinutes > 0)
                    expiryTime = DateTime.Now.AddMinutes(dto.ExpiryMinutes);
            }
            else
            {
                // you may still allow expiry for one_time/multi — keep if dto.ExpiryMinutes > 0
                if ((qrType == "one_time" || qrType == "multi") && dto.ExpiryMinutes > 0)
                    expiryTime = DateTime.Now.AddMinutes(dto.ExpiryMinutes);
            }

            // 3) decide maxUses
            int? maxUses = null;
            if (qrType == "one_time")
            {
                maxUses = dto.MaxUses.HasValue ? (dto.MaxUses.Value <= 0 ? 1 : dto.MaxUses.Value) : 1;
            }
            else if (qrType == "multi")
            {
                if (!dto.MaxUses.HasValue || dto.MaxUses <= 0)
                    throw new ApplicationException("MaxUses must be provided and > 0 for 'multi' QR type.");
                maxUses = dto.MaxUses;
            }
            else
            {
                // time/unlimited -> leave maxUses as null (unlimited) unless caller explicitly provided MaxUses
                maxUses = dto.MaxUses;
            }

            // 4) build payload to SP — use ISO 8601 (round-trip) for expiry if present
            var payload = new
            {
                visitorname = dto.VisitorName,
                phone = dto.Phone,
                purpose = dto.Purpose,
                propertyid = dto.PropertyId,
                buildingid = dto.BuildingId,
                flatid = dto.FlatId,
                requestedby = dto.RequestedBy,
                qrcode = token,
                expiry = expiryTime?.ToString("o"), // "o" is ISO 8601 round-trip (UTC includes Z)
                qr_type = qrType,
                max_uses = maxUses
            };

            // 5) call repo to insert
            var insertedId = await _repo.CreateVisitorRequestAsync(payload);

            // 6) generate QR PNG base64
            var qrBase64 = QrHelper.GenerateQrBase64Png(token);

            return new CreatedVisitorResult
            {
                Id = insertedId,
                QrToken = token,
                QrImageBase64 = qrBase64,
                ExpiryUtc = expiryTime // nullable: null means no expiry
            };
        }

        public async Task<object?> GetVisitorByIdAsync(int id)
        {
            if (id <= 0) throw new ApplicationException("Invalid id.");

            var json = await _repo.GetVisitorByIdRawAsync(id);

            // parse wrapper
            var root = json.RootElement;
            var status = root.GetProperty("status_code").GetInt32();
            if (status != 200) return null;

            var data = root.GetProperty("data");
            JsonElement el = data.ValueKind == JsonValueKind.Array ? data[0] : data;

            // return a dynamic object or a DTO — keep flexible
            return new
            {
                id = el.GetProperty("id").GetInt32(),
                visitorname = el.GetProperty("visitorname").GetString(),
                phone = el.GetProperty("phone").GetString(),
                purpose = el.GetProperty("purpose").GetString(),
                propertyid = el.GetProperty("propertyid").GetInt32(),
                buildingid = el.GetProperty("buildingid").GetInt32(),
                flatid = el.GetProperty("flatid").GetInt32(),
                qrcode = el.GetProperty("qrcode").GetString(),
                status = el.GetProperty("status").GetString(),
                expiry = el.TryGetProperty("expiry", out var ex) && ex.ValueKind != JsonValueKind.Null ? ex.GetDateTime() : (DateTime?)null
            };
        }
        public async Task<int> CreatePGMembers(CreateFlatOwnerModel dto)
        {
            // Basic validation
            if (dto.PropertyId <= 0) throw new ApplicationException("Invalid PropertyId.");
            if (dto.BuildingId <= 0) throw new ApplicationException("Invalid BuildingId.");
            if (dto.FlatNo == null) throw new ApplicationException("Invalid FlatNo.");
            // if (dto.UserId <= 0) throw new ApplicationException("Invalid UserId.");

            int finalUserId = dto.UserId ?? 0;

            if (finalUserId <= 0)
            {
                // Check if email is provided
                if (string.IsNullOrWhiteSpace(dto.Email))
                    throw new ApplicationException("Email is required when UserId is not provided.");

                // Check if user exists
                var existingUser = await _authRepo.GetByEmailAsync(dto.Email);
                if (existingUser != null)
                {
                    finalUserId = existingUser.Id;
                }
                else
                {
                    // Create new user
                    if (string.IsNullOrWhiteSpace(dto.Firstname)) throw new ApplicationException("Firstname is required for new user.");
                    if (string.IsNullOrWhiteSpace(dto.Password)) throw new ApplicationException("Password is required for new user.");

                    var newUser = new UserModel
                    {
                        Firstname = dto.Firstname,
                        Middlename = dto.Middlename ?? "",
                        Lastname = dto.Lastname ?? "",
                        Email = dto.Email,
                        Phone = dto.Phone ?? "",
                        Password = dto.Password,
                        IsActive = true,
                        CreatedBy = dto.CreatedBy
                    };

                    finalUserId = await _authRepo.CreateAsync(newUser);
                }
                dto.UserId = finalUserId;
            }
            if (dto.RoleId <= 0) throw new ApplicationException("Invalid RoleId.");
            if (string.IsNullOrWhiteSpace(dto.GuestType) ||!AllowedGuestTypes.Contains(dto.GuestType))
            {
                throw new ApplicationException(
                    "Invalid guest type."
                );
            }

            var id = await _repo.CreatePGRepoAsync(dto);

            // Create a permanent QR for this PG member (mirror flat owner behavior)
            try
            {
                var token = QrHelper.GenerateToken();

                var visitorPayload = new
                {
                    visitorname = "Self",
                    phone = "",
                    purpose = "Permanent QR for PG member",
                    propertyid = dto.PropertyId,
                    buildingid = dto.BuildingId,
                    flatid = dto.FlatNo,
                    userid = dto.UserId,
                    qrcode = token,
                    qr_type = "unlimited",
                    flatownerid = dto.UserId
                };

                await _repo.CreateVisitorRequestAsync(visitorPayload);
            }
            catch (Exception ex)
            {
                throw new ApplicationException("PG member created but failed to create permanent QR: " + ex.Message);
            }

            return id;
        }

        public async Task<ServiceResult<IEnumerable<dynamic>>> GetPGMembersAsync(PGMemberRequest request)
        {
            var data = await _repo.GetPGMembersAsync(request);
            return ServiceResult<IEnumerable<dynamic>>.Success(data, "PG members fetched successfully.");
        }

        public async Task<ServiceResult<bool>> DeletePGMemberAsync(DeletePGRequest request)
        {
            var success = await _repo.DeletePGMemberAsync(request);
            return success ? ServiceResult<bool>.Success(true, "PG member removed successfully.")
                           : ServiceResult<bool>.Fail("Failed to remove PG member. Verify all details.");
        }

        public async Task<ServiceResult<IEnumerable<dynamic>>> GetHistoryAsync(QRHistoryRequest request)
        {
            var data = await _repo.GetUserQRHistoryAsync(request);
            return ServiceResult<IEnumerable<dynamic>>.Success(data, "QR History retrieved.");
        }

        public async Task<ServiceResult<bool>> RevokeAsync(RevokeQRRequest request)
        {
            var success = await _repo.RevokeQRAsync(request);
            return success ? ServiceResult<bool>.Success(true, "QR Revoked successfully.")
                           : ServiceResult<bool>.Fail("Failed to revoke QR. It may already be revoked or expired.");
        }

        public async Task RegisterDeviceTokenAsync(UserDeviceTokenModel dto)
        {
            // Basic validation
            if (dto.UserId <= 0) throw new ApplicationException("Invalid UserId.");
            if (string.IsNullOrWhiteSpace(dto.DeviceToken)) throw new ApplicationException("Device token is required.");
            if (string.IsNullOrWhiteSpace(dto.Platform)) throw new ApplicationException("Platform is required.");

            // Call Repository
            var resultJson = await _repo.SaveDeviceTokenRepoAsync(dto);

            // Parse wrapper
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;

            var status = root.GetProperty("status_code").GetInt32();
            var message = root.TryGetProperty("message", out var m) ? m.GetString() : "Unknown error";

            if (status != 200)
            {
                throw new ApplicationException(message);
            }
        }

        public async Task ApproveVisitorRequestAsync(VisitorApprovalRequest req)
        {
            if (req.RequestId <= 0) throw new ApplicationException("Invalid RequestId.");

            // 1. Get request details to know the Guard (RequestedBy)
            var visitorJson = await _repo.GetVisitorByIdRawAsync(req.RequestId);
            var root = visitorJson.RootElement;
            if (root.GetProperty("status_code").GetInt32() != 200)
                throw new ApplicationException("Visitor request not found.");

            var dataArr = root.GetProperty("data");
            var item = dataArr.ValueKind == JsonValueKind.Array ? dataArr[0] : dataArr;
            
            int requestedBy = item.GetProperty("requestedby").GetInt32();
            string visitorName = item.GetProperty("visitorname").GetString() ?? "Visitor";

            // 2. Logic based on Approval Status
            bool isApproved = req.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase);

            if (isApproved)
            {
                // A) Update Status to 'Approved'
                var updatePayload = new
                {
                    id = req.RequestId,
                    status = "Active",
                    modifiedby = req.ApprovedBy
                };
                using var updDoc = await _securityRepo.UpdateVisitorRequestStatusRawAsync(updatePayload);
                if (updDoc.RootElement.GetProperty("status_code").GetInt32() != 200)
                    throw new ApplicationException("Failed to update visitor status.");

                // B) Create Visitor Log
                var logPayload = new
                {
                    visitorrequestid = req.RequestId,
                    securityid = requestedBy, // Security who requested it is responsible for "entry" in this flow
                    entrytime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    remarks = req.Remarks
                };
                using var logDoc = await _securityRepo.CreateVisitorLogRawAsync(logPayload);
                if (logDoc.RootElement.GetProperty("status_code").GetInt32() != 201)
                    throw new ApplicationException("Failed to create visitor log.");
            }
            else
            {
                // Rejected
                var updatePayload = new
                {
                    id = req.RequestId,
                    status = "Expired",
                    modifiedby = req.ApprovedBy
                };
                using var updDoc = await _securityRepo.UpdateVisitorRequestStatusRawAsync(updatePayload);
            }

            // 3. Notify Security Guard
            var guardToken = await _userRepo.GetDeviceTokenAsync(requestedBy);
            if (!string.IsNullOrEmpty(guardToken))
            {
                string title = isApproved ? "Visitor Approved" : "Visitor Rejected";
                string body = isApproved 
                    ? $"{visitorName} has been approved. Log created." 
                    : $"{visitorName} has been rejected.";

                await _pushHelper.SendToDeviceAsync(
                    guardToken,
                    title,
                    body,
                    new Dictionary<string, string>
                    {
                        { "visitorRequestId", req.RequestId.ToString() },
                        { "status", req.Status },
                        { "type", "approval_result" }
                    }
                );
            }
        }
    }
}
