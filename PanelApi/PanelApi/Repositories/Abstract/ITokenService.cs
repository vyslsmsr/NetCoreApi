using PanelApi.Models.DTO;
using System.Security.Claims;

namespace PanelApi.Repositories.Abstract
{
    public interface ITokenService
    {
        /// <summary>
        /// refresh token ayarladık.
        /// </summary>
        TokenResponse GetToken(IEnumerable<Claim> claim);
        string GetRefreshToken();
        ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
    }
}
