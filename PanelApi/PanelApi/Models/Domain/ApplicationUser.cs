using Microsoft.AspNetCore.Identity;

namespace PanelApi.Models.Domain
{
    public class ApplicationUser:IdentityUser
    {
        public string? Name { get; set; }
    }
}
