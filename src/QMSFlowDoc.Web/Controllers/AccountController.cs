using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QMSFlowDoc.Domain.Identity;
using System.ComponentModel.DataAnnotations;

namespace QMSFlowDoc.Web.Controllers
{
    [Route("account")]
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AccountController(SignInManager<ApplicationUser> signInManager)
        {
            _signInManager = signInManager;
        }

        [HttpPost("login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login([FromForm] LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(model.Username, model.Password, model.RememberMe, lockoutOnFailure: true);
                if (result.Succeeded)
                {
                    return LocalRedirect(model.ReturnUrl ?? "/");
                }

                if (result.IsLockedOut)
                {
                    return Redirect($"/login?error=Cuenta bloqueada temporalmente por múltiples intentos fallidos. Inténtelo en 15 minutos.");
                }
                
                return Redirect($"/login?error=Credenciales incorrectas");
            }

            return Redirect($"/login?error=Please provide username and password");
        }

        [HttpGet("logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return LocalRedirect("/login");
        }
    }

    public class LoginViewModel
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
        public string? ReturnUrl { get; set; }
    }
}
