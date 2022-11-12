using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PanelApi.Models;
using PanelApi.Models.Domain;
using PanelApi.Models.DTO;
using PanelApi.Repositories.Abstract;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace PanelApi.Controllers
{
    [Route("api/[controller]/{action}")]
    [ApiController]
    public class AuthorizationController : ControllerBase
    {

        private readonly DatabaseContext _context;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly ITokenService _tokenService;
        public AuthorizationController(DatabaseContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ITokenService tokenService
            )
        {
            this._context = context;
            this.userManager = userManager;
            this.roleManager = roleManager;
            this._tokenService = tokenService;
        }


        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordModel model)
        {
            var status = new Status();
            // tüm alanların dolu değilse geriye dönecek cevap
            if (!ModelState.IsValid)
            {
                status.StatusCode = 0;
                status.Message = "please pass all the valid fields";
                return Ok(status);
            }
            // değişiklik yapılacak kullanıcıyı buluyoruz.
            var user = await userManager.FindByNameAsync(model.Username);
            if (user is null)
            {
                status.StatusCode = 0;
                status.Message = "invalid username";
                return Ok(status);
            }
            // girilen şifreyi kontrol ediyoruz.
            if (!await userManager.CheckPasswordAsync(user, model.CurrentPassword))
            {
                status.StatusCode = 0;
                status.Message = "invalid current password";
                return Ok(status);
            }

            // burada ise şfre değişikliği yapıyoruz.
            var result = await userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (!result.Succeeded)
            {
                status.StatusCode = 0;
                status.Message = "Failed to change password";
                return Ok(status);
            }
            status.StatusCode = 1;
            status.Message = "Password has changed successfully";
            return Ok(result);
        }



        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            var user = await userManager.FindByNameAsync(model.Username);
            // kullanıcı girişi doğrulama
            if (user != null && await userManager.CheckPasswordAsync(user, model.Password))
            {
                /// burada kullanıcının rolünü yakalıyoruz.
                var userRoles = await userManager.GetRolesAsync(user);
                /// bu alanda ise bir talep listesi oluşturacaz.
                var authClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.UserName),
                    // burada rastgele bir guid belirliyoruz.
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                };
                foreach (var userRole in userRoles)
                {
                    authClaims.Add(new Claim(ClaimTypes.Role, userRole));
                }
                /// token ve refresh token belirledik.
                var token = _tokenService.GetToken(authClaims);
                var refreshToken = _tokenService.GetRefreshToken();
                // bu alanda gelen kullanıcıyı veritabanında doğruladık.
                var tokenInfo = _context.TokenInfo.FirstOrDefault(a => a.Usename == user.UserName);
                // tokeninfo boş ise yeni bir refresh token oluşturup ve bu tokena zaman ataması yaptık.
                if (tokenInfo == null)
                {
                    var info = new TokenInfo
                    {
                        Usename = user.UserName,
                        RefreshToken = refreshToken,
                        RefreshTokenExpiry = DateTime.Now.AddDays(1) // burada token süresini 1 gün olarak belirledik.
                    };
                    _context.TokenInfo.Add(info);
                }
                // kullanıcı token bilgisi varsa buraya düşecek
                else
                {
                    tokenInfo.RefreshToken = refreshToken;
                    tokenInfo.RefreshTokenExpiry = DateTime.Now.AddDays(1);
                }
                try
                {
                    _context.SaveChanges();
                }
                catch (Exception ex)
                {
                    return BadRequest(ex.Message);
                }

                /// burada oturum açma için bir nesne oluşturduk ve geriye döndük.
                return Ok(new LoginResponse
                {
                    Name = user.Name,
                    Username = user.UserName,
                    Token = token.TokenString,
                    RefreshToken = refreshToken,
                    Expiration = token.ValidTo,
                    StatusCode = 1,
                    Message = "Logged in"
                });

            }

            //Giriş başarısız olursa 
            return Ok(
                new LoginResponse
                {
                    StatusCode = 0,
                    Message = "Invalid Username or Password",
                    Token = "",
                    Expiration = null
                });
        }




        /// yeni kullanıcı oluşturma
        [HttpPost]
        public async Task<IActionResult> Registration([FromBody] RegistrationModel model)
        {
            var status = new Status();
            // tüm alanlar dolu değilse geriye dönecek mesaj
            if (!ModelState.IsValid)
            {
                status.StatusCode = 0;
                status.Message = "Please pass all the required fields";
                return Ok(status);
            }
            // kullanıcının olup olmadığını kontrol ediyoruz
            var userExists = await userManager.FindByNameAsync(model.Username);
            if (userExists != null)
            {
                status.StatusCode = 0;
                status.Message = "Invalid username";
                return Ok(status);
            }
            // kullanıcıyı oluşturduk
            var user = new ApplicationUser
            {
                UserName = model.Username,
                SecurityStamp = Guid.NewGuid().ToString(),
                Email = model.Email,
                Name = model.Name
            };
            // yeni bir uygulama kullanıcısı oluşturuyoruz.
            var result = await userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                status.StatusCode = 0;
                status.Message = "User creation failed";
                return Ok(status);
            }

            // Rol eklemesi
            // kayıt yaparken kullanıcı admin yetkisine sahipmi onu kontrol ediyoruz.
            if (!await roleManager.RoleExistsAsync(UserRoles.User))
                await roleManager.CreateAsync(new IdentityRole(UserRoles.User));

            if (await roleManager.RoleExistsAsync(UserRoles.User))
            {
                await userManager.AddToRoleAsync(user, UserRoles.User);
            }
            status.StatusCode = 1;
            status.Message = "Sucessfully registered";
            return Ok(status);

        }


        // bu alanda admin oluşturuyoruz bunu sadece bir defa yapacaz.
        //[HttpPost]
        //public async Task<IActionResult> RegistrationAdmin([FromBody] RegistrationModel model)
        //{
        //    var status = new Status();
        //    if (!ModelState.IsValid)
        //    {
        //        status.StatusCode = 0;
        //        status.Message = "Please pass all the required fields";
        //        return Ok(status);
        //    }
        //    // kullanıcının var olup olmadığını kontrol ediyouruz
        //    var userExists = await userManager.FindByNameAsync(model.Username);
        //    if (userExists != null)
        //    {
        //        status.StatusCode = 0;
        //        status.Message = "Invalid username";
        //        return Ok(status);
        //    }
        //    var user = new ApplicationUser
        //    {
        //        UserName = model.Username,
        //        SecurityStamp = Guid.NewGuid().ToString(),
        //        Email = model.Email,
        //        Name = model.Name
        //    };
        //    // burada bir kullanıcı oluşturuyoruz
        //    var result = await userManager.CreateAsync(user, model.Password);
        //    if (!result.Succeeded)
        //    {
        //        status.StatusCode = 0;
        //        status.Message = "User creation failed";
        //        return Ok(status);
        //    }

        //    // tol eklemesi yapıyoruz
        //    // yönetici kaydı için UserRoles.Roles yerine UserRoles.Admin
        //    if (!await roleManager.RoleExistsAsync(UserRoles.Admin))
        //        await roleManager.CreateAsync(new IdentityRole(UserRoles.Admin));

        //    if (await roleManager.RoleExistsAsync(UserRoles.Admin))
        //    {
        //        await userManager.AddToRoleAsync(user, UserRoles.Admin);
        //    }
        //    status.StatusCode = 1;
        //    status.Message = "Sucessfully registered";
        //    return Ok(status);

        //}

    }
}
