using System.Security.Claims;
using Isbasi.Web.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Isbasi.Web.Controllers;

[AllowAnonymous]
[Route("account")]
public class AccountController : Controller
{
    // Kullanıcı bulunamadığında da aynı maliyette doğrulama yapılır; yoksa yanıt
    // süresinden hangi e-postaların kayıtlı olduğu anlaşılabilir
    private static readonly string DummyHash = PasswordHasher.Hash(Guid.NewGuid().ToString());

    private readonly AppDbContext _db;
    private readonly LoginThrottle _throttle;

    public AccountController(AppDbContext db, LoginThrottle throttle)
    {
        _db = db;
        _throttle = throttle;
    }

    [HttpGet("login")]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return Redirect("/");
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password, string? returnUrl)
    {
        email ??= "";
        if (_throttle.IsBlocked(email))
        {
            ViewBag.Error = "Çok fazla hatalı deneme yapıldı. Lütfen 15 dakika sonra tekrar deneyin.";
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email);

        bool valid = user != null
            ? PasswordHasher.Verify(password ?? "", user.PasswordHash)
            : PasswordHasher.Verify(password ?? "", DummyHash);

        if (!valid)
        {
            _throttle.RecordFailure(email);
            ViewBag.Error = "E-posta ya da parola hatalı.";
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }
        _throttle.RecordSuccess(email);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user!.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });

        return Redirect(string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl) ? "/" : returnUrl);
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpGet("changepassword")]
    public IActionResult ChangePassword() => View();

    [Authorize]
    [HttpPost("changepassword")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string newPasswordConfirm)
    {
        int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _db.Users.FirstAsync(u => u.Id == userId);

        if (!PasswordHasher.Verify(currentPassword ?? "", user.PasswordHash))
            ViewBag.Error = "Mevcut parolanız hatalı.";
        else if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            ViewBag.Error = "Yeni parola en az 8 karakter olmalıdır.";
        else if (!newPassword.Any(char.IsLetter) || !newPassword.Any(char.IsDigit))
            ViewBag.Error = "Yeni parola en az bir harf ve bir rakam içermelidir.";
        else if (newPassword != newPasswordConfirm)
            ViewBag.Error = "Yeni parolalar birbiriyle uyuşmuyor.";

        if (ViewBag.Error != null) return View();

        user.PasswordHash = PasswordHasher.Hash(newPassword!);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Parolanız değiştirildi.";
        return Redirect("/");
    }
}
