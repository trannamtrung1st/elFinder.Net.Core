using elFinder.Net.AdvancedDemo.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace elFinder.Net.AdvancedDemo.Pages
{
    public class LoginModel : PageModel
    {
        private readonly DataContext _dataContext;

        public LoginModel(DataContext dataContext)
        {
            _dataContext = dataContext;
        }

        [FromForm]
        public string UserName { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPost()
        {
            var user = _dataContext.Users.FirstOrDefault(o => o.UserName == UserName);

            if (user == null) return RedirectToPage("/Login");

            var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
            identity.AddClaim(new Claim(ClaimTypes.Name, $"{user.Id}"));
            identity.AddClaim(new Claim(nameof(user.UserName), user.UserName));

            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return RedirectToPage("/Index");
        }
    }
}
