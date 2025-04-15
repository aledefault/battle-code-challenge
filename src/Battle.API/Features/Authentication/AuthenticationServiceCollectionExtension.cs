using System.Text.Json;
using Battle.API.Options;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Battle.API.Features.Authentication;

public static class AuthenticationServiceCollectionExtension
{
    public static RouteGroupBuilder MapAuthGroup(this IEndpointRouteBuilder builder) => builder.MapGroup("api/v1/authentication");

    public static RouteGroupBuilder WithUrlEndpoint(this RouteGroupBuilder builder)
    {
        // TODO: Validate input data
        builder.MapGet("url", async (
            [FromQuery(Name = "client_id")] string clientId, 
            [FromQuery(Name = "redirect_uri")] string redirectUri, 
            [FromServices] IOptions<KeycloakOptions> keycloakOptions,
            CancellationToken cancellationToken) =>
        {
            var authCodeUrl = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "client_id", keycloakOptions.Value.ClientId }, // At the moment, we ignore the client_id the client give us.
                { "response_type", "code" },
                { "redirect_uri", redirectUri },
                { "scope", "openid" },
                { "state", Guid.NewGuid().ToString() }
            });

            return Results.Redirect($"{keycloakOptions.Value.AuthUrl}?{await authCodeUrl.ReadAsStringAsync(cancellationToken)})");
        }).AllowAnonymous();

        return builder;
    }

    public static RouteGroupBuilder WithTokenEndpoint(this RouteGroupBuilder builder)
    {
        builder.MapPost("token", async (
            HttpRequest request,
            IOptions<KeycloakOptions> keycloakOptions,
            HttpClient httpClient,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            var formData = await request.ReadFormAsync(cancellationToken);
            FormUrlEncodedContent tokenRequest;
            switch (formData["grant_type"])
            {
                case "refresh_token":
                    tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        { "grant_type", "refresh_token" },
                        { "refresh_token", formData["refresh_token"] },
                        { "client_id", keycloakOptions.Value.ClientId },
                        { "client_secret", keycloakOptions.Value.ClientSecret },
                    });
                    break;

                case "authorization_code":
                    tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        { "grant_type", "authorization_code" },
                        { "code", formData["code"] },
                        { "redirect_uri", formData["redirect_uri"] },
                        { "client_id", keycloakOptions.Value.ClientId },
                        { "client_secret", keycloakOptions.Value.ClientSecret }
                    });
                    break;

                default:
                    throw new FormatException("Unknown grant_type");
            }

            var responseAuth = await httpClient.PostAsync(keycloakOptions.Value.TokenUrl, tokenRequest, cancellationToken);
            if (!responseAuth.IsSuccessStatusCode)
            {
                var errorContent = await responseAuth.Content.ReadAsStringAsync(cancellationToken);
                logger.LogInformation("Authentication failed: {StatusCode} - {Reason}.", responseAuth.StatusCode, errorContent);
                throw new Exception("Invalid authentication.");
            }

            // TODO: Validate
            var json = await responseAuth.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            var accessToken = json.GetProperty("access_token").GetString();
            var refreshToken = json.GetProperty("refresh_token").GetString();
            var expiresIn = json.GetProperty("expires_in").GetInt32();
            var refreshExpiresIn = json.GetProperty("refresh_expires_in").GetInt32();

            if (string.IsNullOrEmpty(accessToken))
            {
                var errorContent = await responseAuth.Content.ReadAsStringAsync(cancellationToken);
                logger.LogInformation("Authentication failed - Access token null or empty: {StatusCode} - {Reason}.", responseAuth.StatusCode, errorContent);
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid authentication."));
            }

            return Results.Ok(new
            {
                access_token = accessToken,
                refresh_token = refreshToken,
                expires_in = expiresIn,
                refresh_expires_in = refreshExpiresIn
            });
        }).AllowAnonymous();

        return builder;
    }
    
    public static RouteGroupBuilder WithLogout(this RouteGroupBuilder builder)
    {
        builder.MapGet("logout", async (
            [FromQuery(Name = "post_logout_redirect_uri")] string redirectUri, 
            [FromServices] IOptions<KeycloakOptions> keycloakOptions,
            CancellationToken cancellationToken) =>
        {
            var authCodeUrl = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "client_id", keycloakOptions.Value.ClientId }, // At the moment, we ignore the client_id the client give us.
                { "post_logout_redirect_uri", redirectUri }
            });

            return Results.Redirect($"{keycloakOptions.Value.LogoutUrl}?{await authCodeUrl.ReadAsStringAsync(cancellationToken)}");
        }).AllowAnonymous();

        return builder;
    }
}