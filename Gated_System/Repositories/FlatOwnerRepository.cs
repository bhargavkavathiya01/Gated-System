using Gated_System.Models;
using Npgsql;
using System.Text.Json;

namespace Gated_System.Repositories
{
    public class FlatOwnerRepository : IFlatOwnerRepository
    {
        private readonly NpgsqlConnection _connection;
        private const string FnProperty = @"SELECT public.sp_api_visitorrequest(@p_operation, @p_json)::text;";

        public FlatOwnerRepository(NpgsqlConnection connection) => _connection = connection;

        public async Task<int> CreateVisitorRequestAsync(object payload)
        {
            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(FnProperty, _connection);
                cmd.Parameters.AddWithValue("p_operation", 2);
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull)
                    throw new ApplicationException("Stored-proc returned empty result.");

                var result = scalar.ToString()!;
                using var doc = JsonDocument.Parse(result);
                var root = doc.RootElement;

                var status = root.GetProperty("status_code").GetInt32();
                if (status != 201)
                {
                    var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "Failed creating visitor";
                    throw new ApplicationException(msg);
                }

                var id = root.GetProperty("data").GetProperty("id").GetInt32();
                return id;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public async Task<JsonDocument> GetVisitorByIdRawAsync(int id)
        {
            var payload = new { id };
            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(FnProperty, _connection);
                cmd.Parameters.AddWithValue("p_operation", 5);
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull)
                    throw new ApplicationException("Stored-proc returned empty result.");

                return JsonDocument.Parse(scalar.ToString()!);
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public async Task<int> CreatePGRepoAsync(CreateFlatOwnerModel model)
        {
            // Adjust stored procedure name and operation code as per your DB design.
            const string query = @"SELECT public.sp_api_userrolemaster(@p_operation, @p_json)::text;";

            var payload = new
            {
                propertyid = model.PropertyId,
                buildingid = model.BuildingId,
                flatnumber = model.FlatNo,
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

    }
}
