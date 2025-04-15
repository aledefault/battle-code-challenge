using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Spectre.Console;

public class TokenProvider
{
    private readonly string _cliRedirectUri;
    private string? _accessToken;
    private string? _refreshToken;
    private DateTime? _accessTokenExpiredBy;
    private DateTime? _refreshTokenExpiredBy;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _baseAuthUrl;

    public TokenProvider(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<TokenProvider> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        var grpcEndpoint = configuration["Endpoints:GrpcApiUrl"] ?? throw new NullReferenceException(nameof(configuration));
        _baseAuthUrl = $"{grpcEndpoint}/api/v1/authentication";
        _cliRedirectUri = configuration["Endpoints:CliRedirectUrl"] ?? throw new NullReferenceException(nameof(configuration));
    }

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_accessTokenExpiredBy.HasValue && _accessTokenExpiredBy > DateTime.UtcNow && _accessToken is not null)
            return _accessToken;

        if (_refreshTokenExpiredBy.HasValue && _refreshTokenExpiredBy > DateTime.UtcNow && _refreshToken is not null)
        {
            await RefreshTokensAsync();
            return _accessToken!;
        }

        var uri = new Uri($"{_baseAuthUrl}/url?client_id=&redirect_uri={Uri.EscapeDataString(_cliRedirectUri)}");
        var httpClient = _httpClientFactory.CreateClient("Http2Client");
        var responseUrl = await httpClient.GetAsync(uri, cancellationToken);

        if (responseUrl.StatusCode != HttpStatusCode.Found || responseUrl.Headers.Location is null)
        {
            var errorContent = await responseUrl.Content.ReadAsStringAsync();
            // logger.LogInformation("Authentication failed: {StatusCode} - {Reason}.", responseAuth.StatusCode, errorContent);
            throw new Exception("Invalid authentication.");
        }

        var authUrl = responseUrl.Headers.Location.ToString();
        if (string.IsNullOrEmpty(authUrl))
            throw new Exception("No redirect URL provided by gRPC service.");

        using var listener = GenerateHttpListener();
        listener.Start();

        OpenBrowser(authUrl);
        AnsiConsole.MarkupLine("[yellow]Waiting for authentication...[/]");
        var callbackRequest = await listener.GetContextAsync();
        await StartAuthenticateAsync(authUrl, callbackRequest);
        
        return _accessToken;
    }

    public async Task<bool> LogoutAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is null || _refreshToken is null)
            return true;
        
        var uri = new Uri($"{_baseAuthUrl}/logout?client_id=&post_logout_redirect_uri={Uri.EscapeDataString(_cliRedirectUri)}");
        var httpClient = _httpClientFactory.CreateClient("Http2Client");
        var responseMessage = await httpClient.GetAsync(uri, cancellationToken);

        var logoutUrl = responseMessage.Headers.Location.ToString();
        if (string.IsNullOrEmpty(logoutUrl))
        {
            AnsiConsole.MarkupLine("[red]Can not logout![/]");
            return false;
        }

        using var listener = GenerateHttpListener();
        listener.Start();

        OpenBrowser(logoutUrl);
        AnsiConsole.MarkupLine("[yellow]Waiting for logout...[/]");

        var listenerTask = listener.GetContextAsync();
        var result = await Task.WhenAny(listenerTask, Task.Delay(10_000, cancellationToken));

        if (result.Id != listenerTask.Id)
        {
            AnsiConsole.MarkupLine("[red]Timeout waiting for logout![/]");
            return false;
        }
        
        var callbackResponse = await listener.GetContextAsync();
        await SetCloseMessageInBrowserAsync(callbackResponse.Response, "Logout completed. You may now close this tab.");
        
        _accessToken = null;
        _refreshToken = null;
        _accessTokenExpiredBy = null;
        _refreshTokenExpiredBy = null;

        return true;
    }

    private HttpListener GenerateHttpListener()
    {
        var listener = new HttpListener();
        listener.Prefixes.Add($"{_cliRedirectUri}/");
        return listener;
    }

    private async Task StartAuthenticateAsync(string authCodeUrl, HttpListenerContext httpContext)
    {
        var stateReceived = System.Web.HttpUtility.ParseQueryString(authCodeUrl)["state"]
                            ?? throw new Exception("Authentication error.");

        var code = GetAuthenticationCode(httpContext.Request.Url, stateReceived);
        if (code is null)
        {
            AnsiConsole.MarkupLine("[red]Authentication error.[/]!");
            throw new Exception("Authentication error.");
        }

        await SetTokensAsync(code);
        await SetCloseMessageInBrowserAsync(httpContext.Response, "Login completed. You may now close this tab.");
    }

    private static string? GetAuthenticationCode(Uri? requestUrl, string expectedState)
    {
        if (requestUrl is null)
        {
            AnsiConsole.MarkupLine("[red]Authentication error.[/]!");
            throw new Exception("Authentication error.");
        }

        var queryParams = System.Web.HttpUtility.ParseQueryString(requestUrl.Query);
        var returnedState = queryParams["state"];
        var code = queryParams["code"];
        var error = queryParams["error"];

        if (!string.IsNullOrEmpty(error))
        {
            AnsiConsole.MarkupLine($"[red]Authentication error. Message {error}.[/]!");
            throw new Exception($"Authentication error: {error}");
        }

        if (returnedState != expectedState)
        {
            AnsiConsole.MarkupLine("[red]Authentication error.[/]!");
            throw new Exception($"Authentication error: Invalid state: {returnedState} - {expectedState}");
        }

        return code;
    }

    private async Task SetTokensAsync(string? code)
    {
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "code", code },
            { "redirect_uri", _cliRedirectUri }
        });

        await CallForTokenAsync(tokenRequest);
    }

    private async Task RefreshTokensAsync()
    {
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "refresh_token", _refreshToken },
            { "client_id", "cli" }
        });

        await CallForTokenAsync(tokenRequest);
    }

    private async Task CallForTokenAsync(FormUrlEncodedContent tokenRequest)
    {
        var responseAuth = await _httpClientFactory.CreateClient("Http2Client").PostAsync($"{_baseAuthUrl}/token", tokenRequest);

        if (!responseAuth.IsSuccessStatusCode)
        {
            AnsiConsole.MarkupLine("[red]Authentication error.[/]!");
            throw new Exception($"Authentication error: {responseAuth.ReasonPhrase}");
        }
        
        var json = await responseAuth.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = json.GetProperty("access_token").GetString();
        var refreshToken = json.GetProperty("refresh_token").GetString();
        var expiresIn = json.GetProperty("expires_in").GetInt32();
        var refreshExpiresIn = json.GetProperty("refresh_expires_in").GetInt32();

        _accessToken = accessToken;
        _accessTokenExpiredBy = DateTime.UtcNow.AddSeconds(expiresIn);

        _refreshToken = refreshToken;
        _refreshTokenExpiredBy = DateTime.UtcNow.AddSeconds(refreshExpiresIn);
    }

    private static async Task SetCloseMessageInBrowserAsync(HttpListenerResponse response, string message)
    {
        var responseString = $"<html><body>{message}.</body></html>";
        var buffer = Encoding.UTF8.GetBytes(responseString);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        response.Close();
    }

    private void OpenBrowser(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}