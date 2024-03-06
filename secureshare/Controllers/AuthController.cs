using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using secureshare.Models;
using System.Linq;
using System.Security.Claims;

namespace secureshare.Controllers
{
    public class AuthController : Controller
    {
        private readonly secureshareContext dbContext;

        public AuthController(secureshareContext context)
        {
            dbContext = context;
        }

        public IActionResult Login()
        {
            // Check if the user is already authenticated
            if (User.Identity.IsAuthenticated)
            {

                var userTypeClaim = User.FindFirst(ClaimTypes.Role);


                if(userTypeClaim.ToString() == "User")
                {
                    return RedirectToAction("Index", "User");
                }
                else if(userTypeClaim.ToString() == "Admin")
                {
                    return RedirectToAction("Index", "Admin");
                }
  
            }

            // If not authenticated, show the login view
            return View(); // Load the login view initially
        }

        [HttpPost]
        public async Task<IActionResult> Login(string userType, string username, string password)
        {
            if (userType == "User")
            {
                // Authenticate the user based on username and password for User
                var user = AuthenticateUser(username, password);

                if (user != null)
                {
                    // Create claims for the user
                    var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new Claim(ClaimTypes.Role, "User"),
                new Claim("UserId", user.UserID.ToString())
            };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = true // Set true for a persistent cookie
                    };

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);

                    return RedirectToAction("Index", "User"); // Redirect to the user dashboard after successful login
                }
            }
            else if (userType == "Admin")
            {
                // Authenticate the user based on username and password for Admin
                var admin = AuthenticateAdmin(username, password);

                if (admin != null)
                {
                    // Create claims for the admin
                    var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, admin.AdminID.ToString()),
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim("AdminId", admin.AdminID.ToString())
            };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = true // Set true for a persistent cookie
                    };

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);

                    return RedirectToAction("Index", "Admin"); // Redirect to the admin dashboard after successful login
                }
            }

            // Authentication failed, show an error message
            ViewBag.ErrorMessage = "Invalid username or password";


            // Reload the login view with an error message
            return View("Login");
        }

        private User AuthenticateUser(string username, string password)
        {
            // Perform authentication logic here for User, e.g., query the database
            var user = dbContext.Users.FirstOrDefault(u => u.Username == username && u.Password == password);

            return user;
        }

        private Admin AuthenticateAdmin(string username, string password)
        {
            // Perform authentication logic here for Admin, e.g., query the database
            var admin = dbContext.Admins.FirstOrDefault(a => a.Username == username && a.Password == password);

            return admin;
        }


        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Auth");
        }



        public IActionResult Register()
        {

            // If not authenticated, show the registration view
            return View(); // Load the registration view initially
        }

        [HttpPost]
        public async Task<IActionResult> Register(User user)
        {


            // Validate the user's input
            if (ModelState.IsValid)
            {

                var User = await dbContext.Users.FirstOrDefaultAsync(ufp => ufp.Username== user.Username);

                if (User != null){
                    return View(user);
                }

                user.PermissionType = 2; 
                dbContext.Users.Add(user);
                await dbContext.SaveChangesAsync();

                // Create claims for the registered user
                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
            new Claim(ClaimTypes.Role, "User"),
            new Claim("UserId", user.UserID.ToString())
        };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true // Set true for a persistent cookie
                };

                HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties).Wait(); // Use Wait() to perform the sign-in synchronously

                return RedirectToAction("Index", "User"); // Redirect to the user dashboard after successful registration
            }

            // If the input is not valid, reload the registration view with validation errors
            return View(user);
        }
    }
}
