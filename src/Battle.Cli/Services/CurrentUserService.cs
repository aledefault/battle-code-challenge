using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Battle.Cli.Services;

public class CurrentUserService
{
    private ClaimsPrincipal _currentUser;

    public ClaimsPrincipal CurrentUser => _currentUser;

    public string UserId => _currentUser.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
    
    public void SetCurrentUser(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var claimsIdentity = new ClaimsIdentity(jwtToken.Claims, "Bearer");
        
        if (!claimsIdentity.HasClaim(c => c.Type == ClaimTypes.NameIdentifier))
        {
            var subClaim = claimsIdentity.FindFirst("sub");
            if (subClaim != null)
            {
                claimsIdentity.AddClaim(new Claim(ClaimTypes.NameIdentifier, subClaim.Value));
            }
        }
        
        _currentUser =  new ClaimsPrincipal(claimsIdentity);
    }
}