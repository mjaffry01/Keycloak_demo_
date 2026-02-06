using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "admin")]
[Tags("10 - Admin")]
public class AdminUsersController : ControllerBase
{
    private readonly IHttpClientFactory _http;
    private readonly KeycloakOptions _kc;

    public AdminUsersController(IHttpClientFactory http, IOptions<KeycloakOptions> kc)
    {
        _http = http;
        _kc = kc.Value;
    }

    [HttpPost("create-seller")]
    public async Task<IActionResult> CreateSeller([FromBody] CreateUserRequest req)
    {
        if (req is null) return BadRequest("Body is required.");
        if (string.IsNullOrWhiteSpace(req.Username)) return BadRequest("Username is required.");
        if (string.IsNullOrWhiteSpace(req.Password)) return BadRequest("Password is required.");

        var authHeader = Request.Headers["Authorization"].ToString();
        if (!authHeader.StartsWith("Bearer "))
            return Unauthorized("Missing admin Bearer token.");

        var adminToken = authHeader.Substring("Bearer ".Length);

        if (string.IsNullOrWhiteSpace(_kc.BaseUrl) || string.IsNullOrWhiteSpace(_kc.Realm))
            return StatusCode(500, "Keycloak config missing: Keycloak:BaseUrl and/or Keycloak:Realm");

        var baseUrl = _kc.BaseUrl.TrimEnd('/');
        var realm = _kc.Realm;

        var client = _http.CreateClient();

        // ----------------------------------------------------
        // 1) Create user
        // ----------------------------------------------------
        var createUserPayload = new
        {
            username = req.Username.Trim(),
            enabled = true,
            credentials = new[]
            {
                new { type = "password", value = req.Password, temporary = false }
            }
        };

        var createUserReq = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl}/admin/realms/{realm}/users"
        );
        createUserReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        createUserReq.Content = new StringContent(JsonSerializer.Serialize(createUserPayload), Encoding.UTF8, "application/json");

        var createResp = await client.SendAsync(createUserReq);

        // If user already exists, Keycloak often returns 409
        if ((int)createResp.StatusCode == 409)
            return Conflict($"User '{req.Username}' already exists in Keycloak.");

        if (!createResp.IsSuccessStatusCode)
        {
            var err = await createResp.Content.ReadAsStringAsync();
            return StatusCode((int)createResp.StatusCode, $"Keycloak create user failed: {err}");
        }

        // Extract userId from Location header
        var location = createResp.Headers.Location?.ToString() ?? "";
        var userId = location.Split('/').LastOrDefault();
        if (string.IsNullOrWhiteSpace(userId))
            return Ok(new { message = "User created, but could not parse userId.", username = req.Username });

        // ----------------------------------------------------
        // 2) Ensure realm role "seller" exists (create if missing)
        // ----------------------------------------------------
        async Task<HttpResponseMessage> GetRole(string roleName)
        {
            var roleReq = new HttpRequestMessage(
                HttpMethod.Get,
                $"{baseUrl}/admin/realms/{realm}/roles/{roleName}"
            );
            roleReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            return await client.SendAsync(roleReq);
        }

        async Task<HttpResponseMessage> CreateRole(string roleName)
        {
            var createRoleReq = new HttpRequestMessage(
                HttpMethod.Post,
                $"{baseUrl}/admin/realms/{realm}/roles"
            );
            createRoleReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            createRoleReq.Content = new StringContent(
                JsonSerializer.Serialize(new { name = roleName }),
                Encoding.UTF8,
                "application/json"
            );
            return await client.SendAsync(createRoleReq);
        }

        var roleNameToAssign = "seller";

        var roleResp = await GetRole(roleNameToAssign);

        if (roleResp.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Create role, then fetch again
            var createRoleResp = await CreateRole(roleNameToAssign);
            if (!createRoleResp.IsSuccessStatusCode && (int)createRoleResp.StatusCode != 409)
            {
                var err = await createRoleResp.Content.ReadAsStringAsync();
                return StatusCode((int)createRoleResp.StatusCode,
                    $"User created, but could not create role '{roleNameToAssign}': {err}");
            }

            roleResp = await GetRole(roleNameToAssign);
        }

        if (!roleResp.IsSuccessStatusCode)
        {
            var err = await roleResp.Content.ReadAsStringAsync();
            return StatusCode((int)roleResp.StatusCode,
                $"User created, but could not fetch role '{roleNameToAssign}': {err}");
        }

        var roleJson = await roleResp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(roleJson);

        var roleId = doc.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
        var roleName = doc.RootElement.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;

        if (string.IsNullOrWhiteSpace(roleId) || string.IsNullOrWhiteSpace(roleName))
            return StatusCode(500, $"User created, but role '{roleNameToAssign}' response missing id/name.");

        // ----------------------------------------------------
        // 3) Map role to user
        // ----------------------------------------------------
        var mapPayload = new[]
        {
            new { id = roleId, name = roleName }
        };

        var mapReq = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl}/admin/realms/{realm}/users/{userId}/role-mappings/realm"
        );
        mapReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        mapReq.Content = new StringContent(JsonSerializer.Serialize(mapPayload), Encoding.UTF8, "application/json");

        var mapResp = await client.SendAsync(mapReq);
        if (!mapResp.IsSuccessStatusCode)
        {
            var err = await mapResp.Content.ReadAsStringAsync();
            return StatusCode((int)mapResp.StatusCode, $"User created, but role mapping failed: {err}");
        }

        return Ok(new
        {
            message = "Seller created in Keycloak and assigned role 'seller'.",
            username = req.Username,
            userId,
            roleAssigned = true
        });
    }
}

public class CreateUserRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}
