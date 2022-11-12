using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PanelApi.Models.Domain
{
    public class DatabaseContext: IdentityDbContext<ApplicationUser>
    {   

        /// <summary>
        ///  veri tabanı bağlantısı yapıldı.
        /// </summary>
        public DatabaseContext(DbContextOptions<DatabaseContext> options) :base(options)
        {

        }

        public DbSet<TokenInfo> TokenInfo { get; set; }
    }
}
