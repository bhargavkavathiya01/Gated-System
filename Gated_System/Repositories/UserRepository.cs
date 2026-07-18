using Gated_System.Models;
using Npgsql;
using System.Text.Json;

namespace Gated_System.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly NpgsqlConnection _connection;

        public UserRepository(NpgsqlConnection connection)
        {
            _connection = connection;
        }


        public async Task<bool> UpdateUserAsync(UserProfileUpdateModel user)
        {
            const string query = @"SELECT public.sp_api_usermaster(@p_operation, @p_json)::text;";

            var payload = new
            {
                id = user.Id,
                firstname = user.Firstname,
                middlename = user.Middlename,
                lastname = user.Lastname,
                email = user.Email,
                phone = user.Phone,
                profilepictureurl = user.ProfilePictureUrl,
                modifiedby = user.ModifiedBy
            };

            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(query, _connection);
                cmd.Parameters.AddWithValue("p_operation", 3); // 3 = Update
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalarResult = await cmd.ExecuteScalarAsync();
                if (scalarResult is null) throw new Exception("Database returned no result.");

                using var doc = JsonDocument.Parse(scalarResult.ToString()!);
                var root = doc.RootElement;

                int statusCode = root.GetProperty("status_code").GetInt32();
                string message = root.GetProperty("message").GetString() ?? "";

                // Handle specific business logic errors from SP
                if (statusCode == 409) // Conflict: Email or Phone exists
                {
                    throw new ApplicationException(message);
                }

                return statusCode == 200;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public async Task<bool> UpdatePasswordAsync(UpdatePasswordModel model)
        {
            const string query = @"SELECT public.sp_api_usermaster(@p_operation, @p_json)::text;";

            var payload = new
            {
                id = model.UserId,
                password = model.NewPassword,
                modifiedby = model.ModifiedBy
            };

            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(query, _connection);
                cmd.Parameters.AddWithValue("p_operation", 3);
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalarResult = await cmd.ExecuteScalarAsync();
                if (scalarResult == null) return false;

                using var doc = JsonDocument.Parse(scalarResult.ToString()!);
                // Return true if status_code is 200
                return doc.RootElement.GetProperty("status_code").GetInt32() == 200;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public async Task<bool> UpdateFcmTokenAsync(UpdateFcmTokenModel model)
        {
            const string query = @"SELECT public.sp_api_usermaster(@p_operation, @p_json)::text;";
            const string insertTokenQuery = @"
                INSERT INTO public.tbluserdevicetokens (userid, devicetoken, platform, createdon)
                VALUES (@UserId, @DeviceToken, @Platform, CURRENT_TIMESTAMP)
                ON CONFLICT (userid, devicetoken) DO NOTHING;";

            var payload = new
            {
                id = model.UserId,
                fcmtoken = model.FcmToken,
                modifiedby = model.ModifiedBy
            };

            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(query, _connection);
                cmd.Parameters.AddWithValue("p_operation", 3); // 3 = Update
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalarResult = await cmd.ExecuteScalarAsync();
                if (scalarResult == null) return false;

                using var doc = JsonDocument.Parse(scalarResult.ToString()!);
                var root = doc.RootElement;

                int statusCode = root.GetProperty("status_code").GetInt32();
                string message = root.GetProperty("message").GetString() ?? "";

                if (statusCode == 409)
                    throw new ApplicationException(message);

                if (statusCode != 200) return false;

                // Also insert into tbluserdevicetokens so all push notification flows work
                if (!string.IsNullOrWhiteSpace(model.FcmToken))
                {
                    using var insertCmd = new NpgsqlCommand(insertTokenQuery, _connection);
                    insertCmd.Parameters.AddWithValue("UserId", model.UserId);
                    insertCmd.Parameters.AddWithValue("DeviceToken", model.FcmToken);
                    insertCmd.Parameters.AddWithValue("Platform", string.IsNullOrWhiteSpace(model.Platform) ? "android" : model.Platform);
                    await insertCmd.ExecuteNonQueryAsync();
                }

                return true;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public async Task<string?> GetDeviceTokenAsync(int userId)
        {
            const string query = @"SELECT devicetoken FROM tbluserdevicetokens WHERE userid = @UserId ORDER BY id DESC LIMIT 1";

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(query, _connection);
                cmd.Parameters.AddWithValue("UserId", userId);

                var result = await cmd.ExecuteScalarAsync();
                if (result == null || result == DBNull.Value) return null;
                return result.ToString();
            }
            finally { await _connection.CloseAsync(); }
        }

        public async Task<IEnumerable<UserListResponseModel>> GetAllUsersAsync()
        {
            const string query = @"SELECT public.sp_api_usermaster(@p_operation, @p_json)::text;";

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(query, _connection);
                cmd.Parameters.AddWithValue("p_operation", 1);
                cmd.Parameters.AddWithValue("p_json", DBNull.Value);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull) return Enumerable.Empty<UserListResponseModel>();

                using var doc = JsonDocument.Parse(scalar.ToString()!);
                var root = doc.RootElement;

                if (root.GetProperty("status_code").GetInt32() != 200)
                    throw new ApplicationException(root.GetProperty("message").GetString() ?? "Failed to fetch users.");

                return JsonSerializer.Deserialize<List<UserListResponseModel>>(
                    root.GetProperty("data").GetRawText(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new List<UserListResponseModel>();
            }
            finally { await _connection.CloseAsync(); }
        }

        private const string FnSos = @"SELECT public.sp_api_soscontacts(@p_operation, @p_json)::text;";

        public async Task<int> AddSosContactAsync(int userId, int contactUserId, string? relation)
        {
            var payload = new { userid = userId, contactuserid = contactUserId, relation };
            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(FnSos, _connection);
                cmd.Parameters.AddWithValue("p_operation", 2);
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull) throw new Exception("sp_api_soscontacts returned empty result.");

                using var doc = JsonDocument.Parse(scalar.ToString()!);
                var root = doc.RootElement;
                var statusCode = root.GetProperty("status_code").GetInt32();

                if (statusCode == 409) throw new ApplicationException(root.GetProperty("message").GetString()!);
                if (statusCode != 201) throw new ApplicationException(root.GetProperty("message").GetString() ?? "Failed to add SOS contact.");

                return root.GetProperty("data").GetProperty("id").GetInt32();
            }
            finally { await _connection.CloseAsync(); }
        }

        public async Task<IEnumerable<SosContactResponseModel>> GetSosContactsAsync(int userId)
        {
            var payload = new { userid = userId };
            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(FnSos, _connection);
                cmd.Parameters.AddWithValue("p_operation", 1);
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull) return Enumerable.Empty<SosContactResponseModel>();

                using var doc = JsonDocument.Parse(scalar.ToString()!);
                var root = doc.RootElement;

                if (root.GetProperty("status_code").GetInt32() != 200)
                    throw new ApplicationException(root.GetProperty("message").GetString() ?? "Failed to fetch SOS contacts.");

                return JsonSerializer.Deserialize<List<SosContactResponseModel>>(
                    root.GetProperty("data").GetRawText(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new List<SosContactResponseModel>();
            }
            finally { await _connection.CloseAsync(); }
        }

        public async Task<bool> RemoveSosContactAsync(int id, int userId)
        {
            var payload = new { id, userid = userId };
            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(FnSos, _connection);
                cmd.Parameters.AddWithValue("p_operation", 3);
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull) return false;

                using var doc = JsonDocument.Parse(scalar.ToString()!);
                var root = doc.RootElement;
                var statusCode = root.GetProperty("status_code").GetInt32();

                if (statusCode == 404) throw new ApplicationException(root.GetProperty("message").GetString()!);
                return statusCode == 200;
            }
            finally { await _connection.CloseAsync(); }
        }

        public async Task<bool> UpdateSosRelationAsync(int id, int userId, string? relation)
        {
            var payload = new { id, userid = userId, relation };
            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(FnSos, _connection);
                cmd.Parameters.AddWithValue("p_operation", 4);
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull) return false;

                using var doc = JsonDocument.Parse(scalar.ToString()!);
                var root = doc.RootElement;
                var statusCode = root.GetProperty("status_code").GetInt32();

                if (statusCode == 404) throw new ApplicationException(root.GetProperty("message").GetString()!);
                return statusCode == 200;
            }
            finally { await _connection.CloseAsync(); }
        }

        public async Task<List<string>> GetSosContactDeviceTokensAsync(int userId)
        {
            var payload = new { userid = userId };
            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(FnSos, _connection);
                cmd.Parameters.AddWithValue("p_operation", 5);
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull) return new List<string>();

                using var doc = JsonDocument.Parse(scalar.ToString()!);
                var root = doc.RootElement;

                if (root.GetProperty("status_code").GetInt32() != 200) return new List<string>();

                return root.GetProperty("data").EnumerateArray()
                    .Select(t => t.GetProperty("devicetoken").GetString() ?? "")
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList();
            }
            finally { await _connection.CloseAsync(); }
        }
    }
}
