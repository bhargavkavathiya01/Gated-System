using Gated_System.Models;
using Npgsql;
using System.Net.NetworkInformation;
using System.Text.Json;

namespace Gated_System.Repositories
{
    public class SuperAdminRepository : ISuperAdminRepository
    {
        private readonly NpgsqlConnection _connection;

        public SuperAdminRepository(NpgsqlConnection connection)
        {
            _connection = connection;
        }

        public async Task<IEnumerable<AdminPropertyViewModel>> GetAllPropertiesAsync()
        {
            const string query = @"SELECT public.sp_api_superadminpropertymaster(@p_operation, @p_json)::text;";

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(query, _connection);
                cmd.Parameters.AddWithValue("p_operation", 1);
                cmd.Parameters.AddWithValue("p_json", DBNull.Value);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull)
                    return Enumerable.Empty<AdminPropertyViewModel>();

                var json = scalar.ToString()!;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // check SP success
                if (root.GetProperty("status_code").GetInt32() != 200)
                {
                    var msg = root.GetProperty("message").GetString();
                    throw new ApplicationException($"SP returned error: {msg}");
                }

                // extract the data array
                var data = root.GetProperty("data").GetRawText();

                return JsonSerializer.Deserialize<List<AdminPropertyViewModel>>(data,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                ) ?? new List<AdminPropertyViewModel>();
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public async Task<IEnumerable<AdminPropertyViewModel>> GetAllPropertiesByStatusAsync(string status)
        {
            const string query = @"SELECT public.sp_api_superadminpropertymaster(@p_operation, @p_json)::text;";

            var payload = new
            {
                verificatiostatus = status   // correct key for your SP
            };

            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(query, _connection);
                cmd.Parameters.AddWithValue("p_operation", 5);
                cmd.Parameters.AddWithValue("p_json", jsonPayload);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull)
                    return Enumerable.Empty<AdminPropertyViewModel>();

                var json = scalar.ToString()!;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.GetProperty("status_code").GetInt32() != 200)
                {
                    var msg = root.GetProperty("message").GetString();
                    throw new ApplicationException($"SP returned error: {msg}");
                }

                var data = root.GetProperty("data").GetRawText();

                return JsonSerializer.Deserialize<List<AdminPropertyViewModel>>(data,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                ) ?? new List<AdminPropertyViewModel>();
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public async Task UpdatePropertyVerificationRepoAsync(UpdatePropertyVerificationModel dto)
        {
            const string Sp = @"SELECT public.sp_api_propertymaster(@p_operation, @p_json)::text;";
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            var allowedStatuses = new[] { "Pending", "Approved", "Rejected" };

            if (!allowedStatuses.Contains(dto.IsVerified, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Invalid verification status '{dto.IsVerified}'. Allowed: Pending, Approved, Rejected.");
            }
            var payload = new
            {
                id = dto.PropertyId,
                isverified = dto.IsVerified,
                //modifiedby = dto.ModifiedBy
            };

            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(Sp, _connection);
                cmd.Parameters.AddWithValue("p_operation", 3); // update operation
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull)
                    throw new ApplicationException("Stored-proc returned empty result.");

                var result = scalar.ToString()!;

                // parse wrapper { status_code, message, ... }
                using var doc = JsonDocument.Parse(result);
                var root = doc.RootElement;

                var status = root.GetProperty("status_code").GetInt32();
                var message = root.TryGetProperty("message", out var m) ? m.GetString() : null;

                if (status < 200 || status >= 300)
                {
                    throw new ApplicationException($"SP error. Status: {status}, Message: {message}");
                }

                // success — nothing to return
                return;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

    }
}
