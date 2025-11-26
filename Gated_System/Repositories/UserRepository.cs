using Gated_System.Models;
using Npgsql;
using System.Text.Json;

namespace Gated_System.Repositories
{
    public class UserRepository :IUserRepository
    {
        private readonly NpgsqlConnection _connection;

        public UserRepository(NpgsqlConnection connection)
        {
            _connection = connection;
        }

        public async Task<int> CreateAsync(UserModel user)
        {
            const string query = @"SELECT public.sp_api_usermaster(@p_operation, @p_json)::text;";

            var payload = new
            {
                firstname = user.Firstname,
                middlename = user.Middlename,
                lastname = user.Lastname,
                email = user.Email,
                phone = user.Phone,
                password = user.Password,   // SP will hash this (MD5 as you said)
                isactive = user.IsActive,
                createdby = user.CreatedBy
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
                    throw new Exception("sp_api_usermaster returned null/empty result.");

                var resultJson = scalarResult.ToString();

                // Expected: {"status_code":201,"message":"User inserted successfully","data":{"id":1}}
                using var doc = JsonDocument.Parse(resultJson!);
                var root = doc.RootElement;

                var id = root
                    .GetProperty("data")
                    .GetProperty("id")
                    .GetInt32();

                return id;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }


        //    public async Task<UserModel?> GetByEmailAsync(string email)
        //    {
        //        const string sql = @"
        //    SELECT 
        //        id,
        //        firstname,
        //        middlename,
        //        lastname,
        //        email,
        //        phone,
        //        password,
        //        password_salt,
        //        isactive
        //    FROM tblusermaster 
        //    WHERE email = @email 
        //    LIMIT 1;
        //";

        //        await _connection.OpenAsync();
        //        try
        //        {
        //            using var cmd = new NpgsqlCommand(sql, _connection);
        //            cmd.Parameters.AddWithValue("email", email);

        //            using var reader = await cmd.ExecuteReaderAsync();
        //            if (!await reader.ReadAsync()) return null;

        //            return new UserModel
        //            {
        //                Id = reader.GetInt32(0),
        //                Firstname = reader.GetString(1),
        //                Middlename = reader.IsDBNull(2) ? "" : reader.GetString(2),
        //                Lastname = reader.GetString(3),
        //                Email = reader.GetString(4),
        //                Phone = reader.IsDBNull(5) ? "" : reader.GetString(5),
        //                //PasswordHash = (byte[])reader["password"],
        //                PasswordSalt = reader["password_salt"] as byte[] ?? [],
        //                IsActive = reader.GetBoolean(8)
        //            };
        //        }
        //        finally
        //        {
        //            await _connection.CloseAsync();
        //        }
        //    }
        public async Task<UserModel?> GetByEmailAsync(string email)
        {
            const string sql = @"SELECT public.sp_api_getbyemail(@p_email)::text;";

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(sql, _connection);
                cmd.Parameters.AddWithValue("p_email", email);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull) return null;

                var json = scalar.ToString();
                if (string.IsNullOrWhiteSpace(json)) return null;

                using var doc = JsonDocument.Parse(json!);
                var root = doc.RootElement;

                var statusCode = root.GetProperty("status_code").GetInt32();
                if (statusCode == 404)
                    return null; // user not found

                if (statusCode != 200)
                {
                    // optional: throw on 500 etc.
                    var msg = root.TryGetProperty("message", out var msgProp)
                        ? msgProp.GetString()
                        : "Error from sp_api_getbyemail";
                    throw new ApplicationException(msg);
                }

                var data = root.GetProperty("data");

                var user = new UserModel
                {
                    Id = data.GetProperty("id").GetInt32(),
                    Firstname = data.GetProperty("firstname").GetString() ?? "",
                    Middlename = data.GetProperty("middlename").ValueKind == JsonValueKind.Null
                                 ? ""
                                 : data.GetProperty("middlename").GetString() ?? "",
                    Lastname = data.GetProperty("lastname").GetString() ?? "",
                    Email = data.GetProperty("email").GetString() ?? "",
                    Phone = data.GetProperty("phone").ValueKind == JsonValueKind.Null
                                 ? ""
                                 : data.GetProperty("phone").GetString() ?? ""
                };

                return user;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public async Task<UserModel?> AuthenticateAsync(string email, string password)
        {
            const string sql = @"SELECT public.sp_api_authenticateuser(@p_json)::text;";

            var payload = new { email, password };
            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(sql, _connection);
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull) return null;

                var resultJson = scalar.ToString();
                using var doc = JsonDocument.Parse(resultJson!);
                var root = doc.RootElement;

                var statusCode = root.GetProperty("status_code").GetInt32();
                if (statusCode != 200) return null;

                var data = root.GetProperty("data");

                return new UserModel
                {
                    Id = data.GetProperty("userid").GetInt32(),
                    Firstname = data.GetProperty("firstname").GetString() ?? "",
                    Middlename = data.GetProperty("middlename").ValueKind == JsonValueKind.Null ? "" : data.GetProperty("middlename").GetString() ?? "",
                    Lastname = data.GetProperty("lastname").GetString() ?? "",
                    Email = data.GetProperty("email").GetString() ?? "",
                    Phone = data.GetProperty("phone").ValueKind == JsonValueKind.Null ? "" : data.GetProperty("phone").GetString() ?? ""
                };
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }


        //public async Task<UserModel?> GetByIdAsync(int Userid)
        //{
        //    const string sql = @"
        //    SELECT id, full_name, email, phone, password, password_salt, is_active
        //    FROM tblusermaster WHERE id = @id LIMIT 1;
        //";

        //    await _connection.OpenAsync();
        //    try
        //    {
        //        using var cmd = new NpgsqlCommand(sql, _connection);
        //        cmd.Parameters.AddWithValue("id", Userid);

        //        using var reader = await cmd.ExecuteReaderAsync();
        //        if (!await reader.ReadAsync()) return null;

        //        return new UserModel
        //        {
        //            Id = reader.GetInt32(0),
        //            Firstname = reader.GetString(1),
        //            Email = reader.GetString(2),
        //            Phone = reader.IsDBNull(3) ? "" : reader.GetString(3),
        //            Password = "",
        //            PasswordSalt = (byte[])reader["password"],
        //            IsActive = reader.GetBoolean(6)
        //        };
        //    }
        //    finally
        //    {
        //        await _connection.CloseAsync();
        //    }
        //}

        public async Task<UserModel?> GetByIdAsync(int userId)
        {
            const string sql = @"SELECT public.sp_api_usermaster(@p_operation, @p_json)::text;";

            // Build JSON payload expected by the SP
            var payload = new { id = userId };
            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(sql, _connection);
                cmd.Parameters.AddWithValue("p_operation", 5);         // 5 = fetch by id
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull) return null;

                var resultJson = scalar.ToString();
                if (string.IsNullOrWhiteSpace(resultJson)) return null;

                using var doc = JsonDocument.Parse(resultJson);
                var root = doc.RootElement;

                var statusCode = root.GetProperty("status_code").GetInt32();
                if (statusCode == 404) return null;
                if (statusCode != 200)
                {
                    var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "Error from sp_api_usermaster";
                    throw new ApplicationException(msg);
                }

                var data = root.GetProperty("data");
                if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0) return null;

                var item = data[0];

                // Helper local funcs for safe extraction
                static string SafeString(JsonElement e, string propName)
                {
                    if (!e.TryGetProperty(propName, out var p)) return string.Empty;
                    if (p.ValueKind == JsonValueKind.Null || p.ValueKind == JsonValueKind.Undefined) return string.Empty;
                    return p.GetString() ?? string.Empty;
                }

                static int SafeInt(JsonElement e, string propName, int defaultValue = 0)
                {
                    if (!e.TryGetProperty(propName, out var p)) return defaultValue;
                    if (p.ValueKind != JsonValueKind.Number) return defaultValue;
                    return p.GetInt32();
                }

                static bool SafeBool(JsonElement e, string propName, bool defaultValue = false)
                {
                    if (!e.TryGetProperty(propName, out var p)) return defaultValue;
                    if (p.ValueKind == JsonValueKind.True) return true;
                    if (p.ValueKind == JsonValueKind.False) return false;
                    return defaultValue;
                }

                // Map fields - adapt to your UserModel
                var user = new UserModel
                {
                    Id = SafeInt(item, "userid", SafeInt(item, "id")), // SP may use 'userid' or 'id'
                    Firstname = SafeString(item, "firstname"),
                    Middlename = SafeString(item, "middlename"),
                    Lastname = SafeString(item, "lastname"),
                    Email = SafeString(item, "email"),
                    Phone = SafeString(item, "phone")
                };

                return user;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }


        //public async Task AssignRoleAsync(int userId, string roleName)
        //{
        //    const string sqlRole = "SELECT id FROM tblrolemaster WHERE rolename = @name LIMIT 1;";
        //    const string sqlInsertUserRole = "INSERT INTO tbuserrolemaster (userid, propertyid, buildingid, flatnumber, roleid) VALUES (@uid, @pid, @bid, @flat, @rid) ON CONFLICT DO NOTHING;";

        //    await _connection.OpenAsync();
        //    try
        //    {
        //        using var tx = await _connection.BeginTransactionAsync();

        //        int roleId;
        //        using (var cmd = new NpgsqlCommand(sqlRole, _connection, tx))
        //        {
        //            cmd.Parameters.AddWithValue("name", roleName);
        //            var res = await cmd.ExecuteScalarAsync();
        //            if (res == null)
        //            {
        //                // Optionally create role
        //                const string createRoleSql = "INSERT INTO tblrolemaster (name) VALUES (@name) RETURNING id;";
        //                using var c2 = new NpgsqlCommand(createRoleSql, _connection, tx);
        //                c2.Parameters.AddWithValue("name", roleName);
        //                roleId = (int)await c2.ExecuteScalarAsync();
        //            }
        //            else roleId = (int)res;
        //        }

        //        using var cmd2 = new NpgsqlCommand(sqlInsertUserRole, _connection, tx);
        //        cmd2.Parameters.AddWithValue("uid", userId);
        //        cmd2.Parameters.AddWithValue("rid", roleId);
        //        await cmd2.ExecuteNonQueryAsync();

        //        await tx.CommitAsync();
        //    }
        //    finally
        //    {
        //        await _connection.CloseAsync();
        //    }
        //}

        public async Task AssignRoleAsync(int userId, int roleId)
        {
            await _connection.OpenAsync();
            try
            {
                using var tx = await _connection.BeginTransactionAsync();

                const string sqlUserRoleSp = @"SELECT sp_api_userrolemaster(@operation, @p_json)::text;";

                var userRolePayload = new
                {
                    userid = userId,
                    propertyid = (int?)null,
                    buildingid = (int?)null,
                    flatnumber = "",
                    roleid = roleId
                };

                string userRoleJson = JsonSerializer.Serialize(userRolePayload);

                using (var cmd2 = new NpgsqlCommand(sqlUserRoleSp, _connection, tx))
                {
                    cmd2.Parameters.AddWithValue("operation", 2); // 2 = insert
                    cmd2.Parameters.AddWithValue("p_json", userRoleJson);

                    var response = await cmd2.ExecuteScalarAsync(); // You can parse JSON if needed
                }

                await tx.CommitAsync();
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }


        //public async Task<IEnumerable<UserPropertyRole>> GetRolesAsync(int userId)
        //{
        //    const string sql = @"
        //    SELECT r.name
        //    FROM roles r
        //    JOIN user_roles ur ON ur.role_id = r.id
        //    WHERE ur.user_id = @uid;
        //";

        //    var roles = new List<string>();
        //    await _connection.OpenAsync();
        //    try
        //    {
        //        using var cmd = new NpgsqlCommand(sql, _connection);
        //        cmd.Parameters.AddWithValue("uid", userId);
        //        using var reader = await cmd.ExecuteReaderAsync();
        //        while (await reader.ReadAsync())
        //        {
        //            roles.Add(reader.GetString(0));
        //        }
        //    }
        //    finally
        //    {
        //        await _connection.CloseAsync();
        //    }

        //    return (IEnumerable<UserPropertyRole>)roles;
        //}
        public async Task<IEnumerable<UserPropertyRole>> GetRolesAsync(int userId)
        {
            const string sql = @"SELECT public.sp_api_getuserrolesbyId(@p_userid)::text;";

            var roles = new List<UserPropertyRole>();

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(sql, _connection);
                cmd.Parameters.AddWithValue("p_userid", userId);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull)
                    return roles;

                var json = scalar.ToString();
                if (string.IsNullOrWhiteSpace(json))
                    return roles;

                using var doc = JsonDocument.Parse(json!);
                var root = doc.RootElement;

                var statusCode = root.GetProperty("status_code").GetInt32();
                if (statusCode != 200)
                {
                    var msg = root.TryGetProperty("message", out var msgProp)
                        ? msgProp.GetString()
                        : "Error from sp_api_getuserrolesbyId";
                    throw new ApplicationException(msg);
                }

                var data = root.GetProperty("data");
                if (data.ValueKind != JsonValueKind.Array)
                    return roles;

                foreach (var item in data.EnumerateArray())
                {
                    int propertyId = item.TryGetProperty("propertyid", out var pid) && pid.ValueKind == JsonValueKind.Number
                        ? pid.GetInt32()
                        : 0;

                    int? buildingId = item.TryGetProperty("buildingid", out var bid) && bid.ValueKind == JsonValueKind.Number
                        ? bid.GetInt32()
                        : null;

                    string flatNumber = item.TryGetProperty("flatnumber", out var fn) && fn.ValueKind == JsonValueKind.String
                        ? fn.GetString() ?? ""
                        : "";

                    int roleId = item.TryGetProperty("roleid", out var rid) && rid.ValueKind == JsonValueKind.Number
                        ? rid.GetInt32()
                        : 0;

                    string roleName = item.TryGetProperty("rolename", out var rn) && rn.ValueKind == JsonValueKind.String
                        ? rn.GetString() ?? ""
                        : "";

                    string? propertyName = item.TryGetProperty("propertyname", out var pn) && pn.ValueKind == JsonValueKind.String
                        ? pn.GetString()
                        : null;

                    string? buildingName = item.TryGetProperty("buildingname", out var bn) && bn.ValueKind == JsonValueKind.String
                        ? bn.GetString()
                        : null;

                    roles.Add(new UserPropertyRole
                    {
                        PropertyId = propertyId,
                        BuildingId = buildingId,
                        FlatNumber = flatNumber,
                        RoleId = roleId,
                        RoleName = roleName,
                        PropertyName = propertyName,
                        BuildingName = buildingName
                    });
                }

                return roles;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }


        //public async Task SaveRefreshTokenAsync(int userId, string token, DateTime expiry)
        //{
        //    const string sql = @"
        //        INSERT INTO refresh_tokens (user_id, token, expiry) VALUES (@uid, @token, @expiry);
        //    ";

        //    await _connection.OpenAsync();
        //    try
        //    {
        //        using var cmd = new NpgsqlCommand(sql, _connection);
        //        cmd.Parameters.AddWithValue("uid", userId);
        //        cmd.Parameters.AddWithValue("token", token);
        //        cmd.Parameters.AddWithValue("expiry", expiry);
        //        await cmd.ExecuteNonQueryAsync();
        //    }
        //    finally
        //    {
        //        await _connection.CloseAsync();
        //    }
        //}

        public async Task SaveRefreshTokenAsync(int userId, string token, DateTime expiry)
        {
            const string sql = @"SELECT public.sp_api_refreshtokens(@p_operation, @p_json)::text;";

            // Build JSON payload for the SP
            var payload = new
            {
                userid = userId,
                token = token,
                // make sure expiry is in UTC or what you expect
                expiry = expiry
            };

            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(sql, _connection);
                cmd.Parameters.AddWithValue("p_operation", 2);              // 2 = insert
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload); // text/json

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull)
                    throw new ApplicationException("sp_api_refreshtokens returned null for insert.");

                var resultJson = scalar.ToString();
                if (string.IsNullOrWhiteSpace(resultJson))
                    throw new ApplicationException("sp_api_refreshtokens returned empty JSON for insert.");

                using var doc = JsonDocument.Parse(resultJson!);
                var root = doc.RootElement;

                var statusCode = root.GetProperty("status_code").GetInt32();
                if (statusCode != 201)
                {
                    var msg = root.TryGetProperty("message", out var msgProp)
                        ? msgProp.GetString()
                        : "Unknown error from sp_api_refreshtokens (insert)";

                    throw new ApplicationException($"Refresh token insert failed. Status: {statusCode}, Message: {msg}");
                }

                // Optional: read created id if you ever need it
                // var data = root.GetProperty("data");
                // var id = data.GetProperty("id").GetInt32();
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        //public async Task<(int id, DateTime expiry)?> GetRefreshTokenAsync(string token)
        //    {
        //        const string sql = @"
        //        SELECT id, user_id, expiry FROM refresh_tokens WHERE token = @token LIMIT 1;
        //    ";
        //        await _connection.OpenAsync();
        //        try
        //        {
        //            using var cmd = new NpgsqlCommand(sql, _connection);
        //            cmd.Parameters.AddWithValue("token", token);
        //            using var reader = await cmd.ExecuteReaderAsync();
        //            if (!await reader.ReadAsync()) return null;
        //            var id = reader.GetInt32(0);
        //            var expiry = reader.GetDateTime(2);
        //            return (id, expiry);
        //        }
        //        finally
        //        {
        //            await _connection.CloseAsync();
        //        }
        //    }

        public async Task<(int id, DateTime expiry)?> GetRefreshTokenAsync(string token)
        {
            const string sql = @"SELECT public.sp_api_refreshtokens(@p_operation, @p_json)::text;";

            var payload = new
            {
                token = token
            };

            string jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(sql, _connection);
                cmd.Parameters.AddWithValue("p_operation", 5);
                cmd.Parameters.AddWithValue("p_json", jsonPayload);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull)
                    return null;

                var resultJson = scalar.ToString();
                if (string.IsNullOrWhiteSpace(resultJson))
                    return null;

                using var doc = JsonDocument.Parse(resultJson);
                var root = doc.RootElement;

                var statusCode = root.GetProperty("status_code").GetInt32();
                if (statusCode != 200)
                    return null;

                var data = root.GetProperty("data");

                // data is an array with one row
                if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
                    return null;

                var item = data[0];

                int Userid = item.GetProperty("userid").GetInt32();
                DateTime expiry = item.GetProperty("expiry").GetDateTime();

                return (Userid, expiry);
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        //public async Task DeleteRefreshTokenAsync(string token)
        //{
        //    const string sql = "DELETE FROM refresh_tokens WHERE token = @token;";
        //    await _connection.OpenAsync();
        //    try
        //    {
        //        using var cmd = new NpgsqlCommand(sql, _connection);
        //        cmd.Parameters.AddWithValue("token", token);
        //        await cmd.ExecuteNonQueryAsync();
        //    }
        //    finally
        //    {
        //        await _connection.CloseAsync();
        //    }
        //}
        public async Task DeleteRefreshTokenAsync(string token)
        {
            const string sql = @"SELECT public.sp_api_refreshtokens(@p_operation, @p_json)::text;";

            var payload = new
            {
                token = token
            };

            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(sql, _connection);
                cmd.Parameters.AddWithValue("p_operation", 4);
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull)
                    return; // nothing else to do

                var resultJson = scalar.ToString();
                if (string.IsNullOrWhiteSpace(resultJson))
                    return;

                using var doc = JsonDocument.Parse(resultJson!);
                var root = doc.RootElement;

                var statusCode = root.GetProperty("status_code").GetInt32();
                if (statusCode != 200)
                {
                    // optional: log or throw if you care
                    var msg = root.TryGetProperty("message", out var msgProp)
                        ? msgProp.GetString()
                        : "Error deleting refresh token";
                    // you can log msg if you want, or:
                    // throw new ApplicationException(msg);
                }
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public async Task<int> CreatePropertyAsync(PropertyCreateModel property)
        {
            const string query = @"SELECT public.sp_api_propertymaster(@p_operation, @p_json)::text;";

            var payload = new
            {
                propertyname = property.PropertyName,
                address = property.Address,
                city = property.City,
                pincode = property.Pincode,
                builderid = property.BuilderId
            };

            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(query, _connection);
                cmd.Parameters.AddWithValue("p_operation", 2); // 2 = insert
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalarResult = await cmd.ExecuteScalarAsync();

                if (scalarResult is null || scalarResult is DBNull)
                    throw new Exception("sp_api_propertymaster returned null/empty result.");

                var resultJson = scalarResult.ToString();

                // Expected: {"status_code":201,"message":"Property created","data":{"id":X}}
                using var doc = JsonDocument.Parse(resultJson!);
                var root = doc.RootElement;

                var statusCode = root.GetProperty("status_code").GetInt32();
                if (statusCode != 201)
                {
                    var message = root.GetProperty("message").GetString();
                    throw new ApplicationException(
                        $"Property insert failed. Status: {statusCode}, Message: {message}");
                }

                var id = root
                    .GetProperty("data")
                    .GetProperty("id")
                    .GetInt32();

                return id;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }
    }
}
