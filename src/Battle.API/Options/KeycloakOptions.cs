namespace Battle.API.Options;

public class KeycloakOptions
{
    public const string Key = "Keycloak";
    
    public string Authority { get; set; }
    public string Audience { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string AdminClientId { get; set; }
    public string AdminClientSecret { get; set; }
    public string AuthUrl { get; set; }
    public string LogoutUrl { get; set; }
    public string AdminApiBaseUrl { get; set; }
    public string TokenUrl { get; set; }
}