using Gated_System.Models;
using Npgsql;
using System.Text.Json;

namespace Gated_System.Repositories
{
    public class SecretoryRepository : ISecretoryRepository
    {
        private readonly NpgsqlConnection _connection;
        private const string FnSocietyImages = @"SELECT public.sp_api_societyimages(@p_operation, @p_json)::text;";

        public SecretoryRepository(NpgsqlConnection connection)
        {
            _connection = connection;
        }

        public async Task<int> CreateCommitteeRepoAsync(CreateCommitteeModel model)
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

        public async Task<int> UploadSocietyImageAsync(int propertyId, string imageUrl, string? imageTitle, int uploadedBy)
        {
            var payload = new { propertyid = propertyId, imageurl = imageUrl, imagetitle = imageTitle, uploadedby = uploadedBy };
            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(FnSocietyImages, _connection);
                cmd.Parameters.AddWithValue("p_operation", 2);
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull) throw new Exception("sp_api_societyimages returned empty result.");

                using var doc = JsonDocument.Parse(scalar.ToString()!);
                var root = doc.RootElement;

                var statusCode = root.GetProperty("status_code").GetInt32();
                if (statusCode != 201)
                    throw new ApplicationException(root.GetProperty("message").GetString() ?? "Failed to upload image.");

                return root.GetProperty("data").GetProperty("id").GetInt32();
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public async Task<IEnumerable<SocietyImageResponseModel>> GetSocietyImagesAsync(int propertyId)
        {
            var payload = new { propertyid = propertyId };
            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(FnSocietyImages, _connection);
                cmd.Parameters.AddWithValue("p_operation", 1);
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull) return Enumerable.Empty<SocietyImageResponseModel>();

                using var doc = JsonDocument.Parse(scalar.ToString()!);
                var root = doc.RootElement;

                if (root.GetProperty("status_code").GetInt32() != 200)
                    throw new ApplicationException(root.GetProperty("message").GetString() ?? "Failed to fetch images.");

                var data = root.GetProperty("data").GetRawText();
                return JsonSerializer.Deserialize<List<SocietyImageResponseModel>>(data,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new List<SocietyImageResponseModel>();
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }
    }
}
