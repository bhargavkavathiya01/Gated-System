using Gated_System.Models;
using Npgsql;
using System.Text.Json;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace Gated_System.Repositories
{
    public class BuilderRepository : IBuilderRepository
    {
        private readonly NpgsqlConnection _connection;

        public BuilderRepository(NpgsqlConnection connection)
        {
            _connection = connection;
        }

        public async Task<IEnumerable<RoleModel>> GetAllRolesAsync()
        {
            const string sql = @"SELECT public.sp_api_rolemaster(@p_operation, @p_json)::text;";

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(sql, _connection);
                cmd.Parameters.AddWithValue("p_operation", 1); // get all
                cmd.Parameters.AddWithValue("p_json", DBNull.Value);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull)
                    return Enumerable.Empty<RoleModel>();

                var json = scalar.ToString();
                if (string.IsNullOrWhiteSpace(json))
                    return Enumerable.Empty<RoleModel>();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.GetProperty("status_code").GetInt32() != 200)
                    return Enumerable.Empty<RoleModel>();

                if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                    return Enumerable.Empty<RoleModel>();

                var roles = new List<RoleModel>();

                foreach (var item in data.EnumerateArray())
                {
                    roles.Add(new RoleModel
                    {
                        Id = item.GetProperty("id").GetInt32(),
                        RoleName = item.GetProperty("rolename").GetString() ?? "",
                        IsActive = item.GetProperty("isactive").GetBoolean()
                    });
                }

                return roles;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }
        public async Task<int> CreateSecretaryRepoAsync(CreateSecretaryModel model)
        {
            // Adjust stored procedure name and operation code as per your DB design.
            const string query = @"SELECT public.sp_api_userrolemaster(@p_operation, @p_json)::text;";

            var payload = new
            {
                propertyid = model.PropertyId,
                userid = model.UserId,
                roleid = model.RoleId,
                createdby = model.CreatedBy
            };

            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(query, _connection);
                cmd.Parameters.AddWithValue("p_operation", 2);
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalarResult = await cmd.ExecuteScalarAsync();

                if (scalarResult is null || scalarResult is DBNull)
                    throw new Exception("returned null/empty result.");

                var resultJson = scalarResult.ToString();

                using var doc = JsonDocument.Parse(resultJson);
                var root = doc.RootElement;

                var statusCode = root.GetProperty("status_code").GetInt32();
                var message = root.GetProperty("message").GetString();

                // 🔴 Duplicate case
                if (statusCode == 409)
                    throw new ApplicationException(message);

                // 🔴 Any DB error
                if (statusCode != 201)
                    throw new Exception(message ?? "Failed to create secretary.");

                // expected: {"status_code":201,"message":"Inserted","data":{"id":123}}
                if (!root.TryGetProperty("data", out var dataEl) || !dataEl.TryGetProperty("id", out var idEl))
                    throw new Exception("Unexpected response from User Role: missing data.id");

                var id = idEl.GetInt32();
                return id;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public async Task<int> CreateFlatOwnerRepoAsync(CreateFlatOwnerModel model)
        {
            // Adjust stored procedure name and operation code as per your DB design.
            const string query = @"SELECT public.sp_api_userrolemaster(@p_operation, @p_json)::text;";

            var payload = new
            {
                propertyid = model.PropertyId,
                buildingid=model.BuildingId,
                flatnumber=model.FlatNo,
                userid = model.UserId,
                roleid = model.RoleId,
                createdby = model.CreatedBy
            };

            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(query, _connection);
                cmd.Parameters.AddWithValue("p_operation", 2);
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalarResult = await cmd.ExecuteScalarAsync();

                if (scalarResult is null || scalarResult is DBNull)
                    throw new Exception("User Role returned null/empty result.");

                var resultJson = scalarResult.ToString();

                using var doc = JsonDocument.Parse(resultJson);
                var root = doc.RootElement;

                // expected: {"status_code":201,"message":"Inserted","data":{"id":123}}
                if (!root.TryGetProperty("data", out var dataEl) || !dataEl.TryGetProperty("id", out var idEl))
                    throw new Exception("Unexpected response from User Role: missing data.id");

                var id = idEl.GetInt32();
                return id;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public async Task<IEnumerable<PropertyViewModel>> GetPropertiesByBuilderIdAsync(int builderId)
        {
            const string query = @"SELECT public.sp_api_propertymaster(@p_operation, @p_json)::text;";

            var payload = new { builderid = builderId };
            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(query, _connection);
                cmd.Parameters.AddWithValue("p_operation", 5);
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalarResult = await cmd.ExecuteScalarAsync();

                if (scalarResult is null || scalarResult is DBNull)
                    return Enumerable.Empty<PropertyViewModel>();

                var resultJson = scalarResult.ToString()!;
                using var doc = JsonDocument.Parse(resultJson);
                var root = doc.RootElement;

                // Validate wrapper
                if (!root.TryGetProperty("status_code", out var statusEl) || statusEl.GetInt32() != 200)
                {
                    var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "Unknown error from SP";
                    throw new ApplicationException($"Property fetch failed. Message: {msg}");
                }

                // If no data or null -> empty list
                if (!root.TryGetProperty("data", out var dataEl) || dataEl.ValueKind == JsonValueKind.Null)
                    return Enumerable.Empty<PropertyViewModel>();

                var list = new List<PropertyViewModel>();

                // data might be an array or a single object — handle both
                if (dataEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in dataEl.EnumerateArray())
                        list.Add(ParsePropertyElement(el));
                }
                else if (dataEl.ValueKind == JsonValueKind.Object)
                {
                    list.Add(ParsePropertyElement(dataEl));
                }
                else
                {
                    // unexpected shape
                    throw new Exception("Unexpected 'data' shape from sp_api_propertymaster");
                }

                return list;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        private static PropertyViewModel ParsePropertyElement(JsonElement el)
        {
            return new PropertyViewModel
            {
                Id = el.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out var idVal) ? idVal : 0,
                PropertyName = el.TryGetProperty("propertyname", out var pnEl) ? pnEl.GetString() ?? "" : "",
                Address = el.TryGetProperty("address", out var addrEl) ? addrEl.GetString() ?? "" : "",
                City = el.TryGetProperty("city", out var cityEl) ? cityEl.GetString() ?? "" : "",
                Pincode = el.TryGetProperty("pincode", out var pinEl) ? pinEl.GetString() ?? "" : "",
                BuilderId = el.TryGetProperty("builderid", out var bEl) && bEl.TryGetInt32(out var bVal) ? bVal : 0,
                IsVerified = el.TryGetProperty("isverified", out var ivEl) ? ivEl.GetString() ?? "" : ""
            };
        }

        public async Task<List<UserListResponseModel>> GetAllUsersAsync()
        {
            const string query = @"SELECT public.sp_api_usermaster(@p_operation, NULL)::text;";
            var users = new List<UserListResponseModel>();

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(query, _connection);
                cmd.Parameters.AddWithValue("p_operation", 1);

                var result = await cmd.ExecuteScalarAsync();
                if (result == null) return users;

                using var doc = JsonDocument.Parse(result.ToString()!);
                var root = doc.RootElement;

                if (root.GetProperty("status_code").GetInt32() != 200)
                    return users;

                if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                    return users;

                foreach (var item in data.EnumerateArray())
                {
                    users.Add(new UserListResponseModel
                    {
                        Id = item.GetProperty("userid").GetInt32(),
                        Firstname = item.GetProperty("firstname").GetString() ?? "",
                        Middlename = item.TryGetProperty("middlename", out var m) ? m.GetString() ?? "" : "",
                        Lastname = item.GetProperty("lastname").GetString() ?? "",
                        Email = item.GetProperty("email").GetString() ?? "",
                        Phone = item.TryGetProperty("phone", out var p) ? p.GetString() ?? "" : "",
                        IsActive = item.GetProperty("isactive").GetBoolean(),
                        RoleId = item.TryGetProperty("roleid", out var rid) ? rid.GetInt32() : null,
                        RoleName = item.TryGetProperty("rolename", out var rn) ? rn.GetString() : null
                    });
                }

                return users;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public async Task<UserResponseModel?> GetUserByEmailOrPhoneAsync(string user)
        {
            const string sql = @"SELECT public.sp_api_getuserbyemailorphone(@p_json)::text;";

            var payload = JsonSerializer.Serialize(new { user });

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(sql, _connection);
                cmd.Parameters.AddWithValue("p_json", payload);

                var result = await cmd.ExecuteScalarAsync();
                if (result == null) return null;

                using var doc = JsonDocument.Parse(result.ToString()!);
                var root = doc.RootElement;

                if (root.GetProperty("status_code").GetInt32() != 200)
                    return null;

                var data = root.GetProperty("data");

                return indicateUser(data);
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        private static UserResponseModel indicateUser(JsonElement data)
        {
            return new UserResponseModel
            {
                Id = data.GetProperty("userid").GetInt32(),
                Firstname = data.GetProperty("firstname").GetString() ?? "",
                Middlename = data.GetProperty("middlename").GetString() ?? "",
                Lastname = data.GetProperty("lastname").GetString() ?? "",
                Email = data.GetProperty("email").GetString() ?? "",
                Phone = data.GetProperty("phone").GetString() ?? ""
            };
        }

        public async Task<PropertyMemberDetailsResponse?> GetPropertyMemberDetailsAsync(PropertyMemberRequest request)
        {
            const string query = @"SELECT public.sp_api_getPropertyWiseMemberDetails(@p_propertyid)::text;";

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(query, _connection);
                cmd.Parameters.AddWithValue("p_propertyid", request.PropertyId);

                var scalarResult = await cmd.ExecuteScalarAsync();

                if (scalarResult == null || scalarResult is DBNull)
                    return null;

                using var doc = JsonDocument.Parse(scalarResult.ToString()!);
                var root = doc.RootElement;

                int statusCode = root.GetProperty("status_code").GetInt32();
                if (statusCode != 200)
                {
                    var msg = root.GetProperty("message").GetString();
                    throw new ApplicationException(msg ?? "Error fetching property details");
                }

                // Deserializing the 'data' property of the JSON result
                var dataJson = root.GetProperty("data").GetRawText();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                return JsonSerializer.Deserialize<PropertyMemberDetailsResponse>(dataJson, options);
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }
    }
}
