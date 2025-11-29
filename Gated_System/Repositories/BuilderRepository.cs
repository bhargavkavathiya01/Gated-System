using Gated_System.Models;
using Npgsql;
using System.Text.Json;

namespace Gated_System.Repositories
{
    public class BuilderRepository : IBuilderRepository
    {
        private readonly NpgsqlConnection _connection;

        public BuilderRepository(NpgsqlConnection connection)
        {
            _connection = connection;
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
                    throw new Exception("sp_api_secretary returned null/empty result.");

                var resultJson = scalarResult.ToString();

                using var doc = JsonDocument.Parse(resultJson);
                var root = doc.RootElement;

                // expected: {"status_code":201,"message":"Inserted","data":{"id":123}}
                if (!root.TryGetProperty("data", out var dataEl) || !dataEl.TryGetProperty("id", out var idEl))
                    throw new Exception("Unexpected response from sp_api_secretary: missing data.id");

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
                    throw new Exception("sp_api_secretary returned null/empty result.");

                var resultJson = scalarResult.ToString();

                using var doc = JsonDocument.Parse(resultJson);
                var root = doc.RootElement;

                // expected: {"status_code":201,"message":"Inserted","data":{"id":123}}
                if (!root.TryGetProperty("data", out var dataEl) || !dataEl.TryGetProperty("id", out var idEl))
                    throw new Exception("Unexpected response from sp_api_secretary: missing data.id");

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

    }
}
