using Gated_System.Helpers;
using Gated_System.Models;
using Gated_System.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Gated_System.Services
{
    public class FlatOwnerService :IFlatOwnerService
    {
        private readonly IFlatOwnerRepository _repo;

        public FlatOwnerService(IFlatOwnerRepository repo) => _repo = repo;

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
            DateTime? expiryUtc = null;
            if (qrType == "time")
            {
                // default behaviour: if ExpiryMinutes==0 treat as no expiry (or you can require >0)
                if (dto.ExpiryMinutes > 0)
                    expiryUtc = DateTime.UtcNow.AddMinutes(dto.ExpiryMinutes);
            }
            else
            {
                // you may still allow expiry for one_time/multi — keep if dto.ExpiryMinutes > 0
                if ((qrType == "one_time" || qrType == "multi") && dto.ExpiryMinutes > 0)
                    expiryUtc = DateTime.UtcNow.AddMinutes(dto.ExpiryMinutes);
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
                expiry = expiryUtc?.ToString("o"), // "o" is ISO 8601 round-trip (UTC includes Z)
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
                ExpiryUtc = expiryUtc // nullable: null means no expiry
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
            if (dto.FlatNo <= 0) throw new ApplicationException("Invalid FlatNo.");
            if (dto.UserId <= 0) throw new ApplicationException("Invalid UserId.");
            if (dto.RoleId <= 0) throw new ApplicationException("Invalid RoleId.");

            var id = await _repo.CreatePGRepoAsync(dto);

            return id;
        }
    }
}
