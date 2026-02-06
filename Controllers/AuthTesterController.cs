using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Linq;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/auth-tester")]
[Tags("90 - Public / Tools")]
public class AuthTesterController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly KeycloakOptions _kc;
    private readonly AuthTesterOptions _tester;

    public AuthTesterController(
        IHttpClientFactory httpClientFactory,
        IOptions<KeycloakOptions> kcOptions,
        IOptions<AuthTesterOptions> testerOptions)
    {
        _httpClientFactory = httpClientFactory;
        _kc = kcOptions.Value;
        _tester = testerOptions.Value;
    }

    public record PasswordGrantRequest(string Username, string Password);
    public record TokenResponse(string AccessToken, int ExpiresIn, string TokenType, string? RefreshToken);

    public record InspectResponse(
        bool LooksLikeJwt,
        int Parts,
        int Length,
        string HeaderJson,
        string PayloadJson,
        string? Iss,
        object? Aud,
        string? Azp,
        string? Sub,
        string? PreferredUsername,
        string? Name,
        DateTimeOffset? IssuedAt,
        DateTimeOffset? ExpiresAt,
        bool? IsExpired,
        double? MinutesRemaining
    );

    public record UserInfoResponse(JsonElement UserInfo);
    public record ApiCallResponse(int StatusCode, string? WwwAuthenticate, string? Body);

    public record FullRunResponse(
        TokenResponse Token,
        InspectResponse Inspect,
        UserInfoResponse? KeycloakUserInfo,
        ApiCallResponse? WhoAmI
    );

    // ----------------------------
    // 1) Get token from Keycloak
    // ----------------------------
    [HttpPost("token")]
    public async Task<ActionResult<TokenResponse>> GetToken([FromBody] PasswordGrantRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest("Username and Password are required.");

        if (string.IsNullOrWhiteSpace(_kc.TokenUrl))
            return StatusCode(500, "Keycloak:TokenUrl is missing from configuration.");

        if (!Uri.TryCreate(_kc.TokenUrl, UriKind.Absolute, out _))
            return StatusCode(500, $"Keycloak:TokenUrl is not a valid absolute URL: '{_kc.TokenUrl}'");

        if (string.IsNullOrWhiteSpace(_kc.ClientId))
            return StatusCode(500, "Keycloak:ClientId is missing from configuration.");

        using var client = _httpClientFactory.CreateClient();

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = _kc.ClientId,
            // Needed for /userinfo
            ["scope"] = "openid profile email",
            ["username"] = req.Username,
            ["password"] = req.Password
        };

        using var content = new FormUrlEncodedContent(form);

        using var resp = await client.PostAsync(_kc.TokenUrl, content);
        var json = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            return StatusCode((int)resp.StatusCode, new { error = "token_request_failed", status = (int)resp.StatusCode, body = json });

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var accessToken = root.GetProperty("access_token").GetString() ?? "";
        var expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 0;
        var tokenType = root.TryGetProperty("token_type", out var tt) ? tt.GetString() ?? "Bearer" : "Bearer";
        var refresh = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

        return Ok(new TokenResponse(accessToken, expiresIn, tokenType, refresh));
    }

    // ----------------------------
    // 2) Inspect token (decode header/payload + exp check)
    // ----------------------------
    [HttpPost("inspect")]
    public ActionResult<InspectResponse> Inspect([FromBody] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest("Token is required.");

        var parts = token.Split('.');
        var looksLikeJwt = parts.Length == 3;

        string headerJson = "";
        string payloadJson = "";

        string? iss = null;
        object? aud = null;
        string? azp = null;
        string? sub = null;
        string? preferredUsername = null;
        string? name = null;

        DateTimeOffset? iat = null;
        DateTimeOffset? exp = null;
        bool? isExpired = null;
        double? minutesRemaining = null;

        if (looksLikeJwt)
        {
            headerJson = DecodeJwtPart(parts[0]);
            payloadJson = DecodeJwtPart(parts[1]);

            try
            {
                using var payloadDoc = JsonDocument.Parse(payloadJson);
                var p = payloadDoc.RootElement;

                iss = p.TryGetProperty("iss", out var issEl) ? issEl.GetString() : null;
                azp = p.TryGetProperty("azp", out var azpEl) ? azpEl.GetString() : null;
                sub = p.TryGetProperty("sub", out var subEl) ? subEl.GetString() : null;
                preferredUsername = p.TryGetProperty("preferred_username", out var uEl) ? uEl.GetString() : null;
                name = p.TryGetProperty("name", out var nEl) ? nEl.GetString() : null;

                if (p.TryGetProperty("aud", out var audEl))
                {
                    aud = audEl.ValueKind == JsonValueKind.Array
                        ? audEl.EnumerateArray().Select(x => x.GetString()).ToArray()
                        : audEl.GetString();
                }

                if (p.TryGetProperty("iat", out var iatEl) && iatEl.TryGetInt64(out var iatSec))
                    iat = DateTimeOffset.FromUnixTimeSeconds(iatSec);

                if (p.TryGetProperty("exp", out var expEl) && expEl.TryGetInt64(out var expSec))
                    exp = DateTimeOffset.FromUnixTimeSeconds(expSec);

                if (exp.HasValue)
                {
                    isExpired = exp.Value <= DateTimeOffset.UtcNow;
                    minutesRemaining = (exp.Value - DateTimeOffset.UtcNow).TotalMinutes;
                }
            }
            catch
            {
                // keep decode info even if JSON parse fails
            }
        }

        return Ok(new InspectResponse(
            LooksLikeJwt: looksLikeJwt,
            Parts: parts.Length,
            Length: token.Length,
            HeaderJson: headerJson,
            PayloadJson: payloadJson,
            Iss: iss,
            Aud: aud,
            Azp: azp,
            Sub: sub,
            PreferredUsername: preferredUsername,
            Name: name,
            IssuedAt: iat,
            ExpiresAt: exp,
            IsExpired: isExpired,
            MinutesRemaining: minutesRemaining
        ));
    }

    // ----------------------------
    // 3) Call Keycloak /userinfo
    // ----------------------------
    [HttpPost("userinfo")]
    public async Task<ActionResult<UserInfoResponse>> UserInfo([FromBody] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest("Token is required.");

        if (string.IsNullOrWhiteSpace(_kc.UserInfoUrl))
            return StatusCode(500, "Keycloak:UserInfoUrl is missing from configuration.");

        using var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var resp = await client.GetAsync(_kc.UserInfoUrl);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            return StatusCode((int)resp.StatusCode, new { error = "userinfo_failed", status = (int)resp.StatusCode, body });

        using var doc = JsonDocument.Parse(body);
        return Ok(new UserInfoResponse(doc.RootElement.Clone()));
    }

    // ----------------------------
    // 4) Call your API /api/whoami
    // ----------------------------
    [HttpPost("whoami")]
    public async Task<ActionResult<ApiCallResponse>> CallWhoAmI([FromBody] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest("Token is required.");

        if (string.IsNullOrWhiteSpace(_tester.ApiBaseUrl))
            return StatusCode(500, "AuthTester:ApiBaseUrl is missing from configuration.");

        var url = $"{_tester.ApiBaseUrl.TrimEnd('/')}/api/whoami";

        using var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var resp = await client.GetAsync(url);
        var body = await resp.Content.ReadAsStringAsync();

        resp.Headers.TryGetValues("WWW-Authenticate", out var wwwVals);
        var www = wwwVals?.FirstOrDefault();

        return Ok(new ApiCallResponse((int)resp.StatusCode, www, body));
    }

    // ----------------------------
    // 5) Do everything like your PS script
    // ----------------------------
    [HttpPost("all")]
    public async Task<ActionResult<FullRunResponse>> All([FromBody] PasswordGrantRequest req)
    {
        var tokenResult = await GetToken(req);
        if (tokenResult.Result is ObjectResult err && err.StatusCode >= 400)
            return tokenResult.Result;

        var token = tokenResult.Value!.AccessToken;

        var inspect = Inspect(token).Value!;

        UserInfoResponse? ui = null;
        try
        {
            var uiRes = await UserInfo(token);
            if (uiRes.Result == null) ui = uiRes.Value;
        }
        catch { }

        ApiCallResponse? who = null;
        try
        {
            var whoRes = await CallWhoAmI(token);
            if (whoRes.Result == null) who = whoRes.Value;
        }
        catch { }

        return Ok(new FullRunResponse(
            Token: tokenResult.Value!,
            Inspect: inspect,
            KeycloakUserInfo: ui,
            WhoAmI: who
        ));
    }

    private static string DecodeJwtPart(string base64Url)
    {
        var b64 = base64Url.Replace('-', '+').Replace('_', '/');
        var pad = 4 - (b64.Length % 4);
        if (pad is > 0 and < 4) b64 = b64 + new string('=', pad);

        var bytes = Convert.FromBase64String(b64);
        return Encoding.UTF8.GetString(bytes);
    }
}
