using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Aetherworks.Services;

public sealed class GoogleSignInService
{
    private static readonly HttpClient Http = new();
    public async Task<string> SignInAsync(string clientId, CancellationToken cancellationToken = default)
    {
        if (!clientId.EndsWith(".apps.googleusercontent.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use a Google OAuth client ID of type Desktop app.");
        using var listener = new HttpListener();
        var port = GetFreePort();
        var redirect = $"http://127.0.0.1:{port}/";
        listener.Prefixes.Add(redirect);
        listener.Start();
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = Base64Url(RandomNumberGenerator.GetBytes(24));
        var authorize = "https://accounts.google.com/o/oauth2/v2/auth?" + FormUrl(new Dictionary<string,string>
        {
            ["client_id"] = clientId, ["redirect_uri"] = redirect, ["response_type"] = "code",
            ["scope"] = "openid email profile", ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256", ["state"] = state, ["access_type"] = "online"
        });
        Process.Start(new ProcessStartInfo(authorize) { UseShellExecute = true });
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        var context = await listener.GetContextAsync().WaitAsync(timeout.Token);
        var code = context.Request.QueryString["code"];
        var returnedState = context.Request.QueryString["state"];
        var error = context.Request.QueryString["error"];
        var html = "<html><body style='font-family:Segoe UI;background:#10131b;color:white;padding:40px'><h2>Aetherworks</h2><p>You can close this window and return to Aetherworks.</p></body></html>";
        var bytes = Encoding.UTF8.GetBytes(html);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
        if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException("Google authorization was denied: " + error);
        if (string.IsNullOrEmpty(code) || returnedState != state) throw new InvalidOperationException("Google sign-in response was invalid.");
        using var tokenResponse = await Http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string,string>
        {
            ["client_id"] = clientId, ["code"] = code, ["code_verifier"] = verifier,
            ["redirect_uri"] = redirect, ["grant_type"] = "authorization_code"
        }), timeout.Token);
        tokenResponse.EnsureSuccessStatusCode();
        using var tokenJson = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(timeout.Token));
        var accessToken = tokenJson.RootElement.GetProperty("access_token").GetString()!;
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        using var userResponse = await Http.SendAsync(request, timeout.Token);
        userResponse.EnsureSuccessStatusCode();
        using var userJson = JsonDocument.Parse(await userResponse.Content.ReadAsStringAsync(timeout.Token));
        if (!userJson.RootElement.TryGetProperty("email_verified", out var verified) || !verified.GetBoolean())
            throw new InvalidOperationException("Google did not return a verified email address.");
        return userJson.RootElement.GetProperty("email").GetString() ?? throw new InvalidOperationException("Google did not return an email address.");
    }
    private static int GetFreePort()
    {
        using var socket = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_');
    private static string FormUrl(Dictionary<string,string> values) => string.Join("&", values.Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value)));
}