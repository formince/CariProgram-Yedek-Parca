using System.Security.Claims;
using CariErinc.Data;
using CariErinc.Models;
using CariErinc.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CariErinc.Controllers;

[AllowAnonymous]
public class AuthController : BaseController
{
    private readonly AppDbContext _db;

    public AuthController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");

        return View(new LoginVM());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVM model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.KullaniciAdi) || string.IsNullOrWhiteSpace(model.Sifre))
        {
            ModelState.AddModelError("", "Kullanıcı adı ve şifre gereklidir.");
            return View(model);
        }

        var kullanici = await _db.Kullanicilar
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.KullaniciAdi == model.KullaniciAdi, cancellationToken);

        if (kullanici == null || !BCrypt.Net.BCrypt.Verify(model.Sifre, kullanici.SifreHash))
        {
            ModelState.AddModelError("", "Kullanıcı adı veya şifre hatalı.");
            return View(model);
        }

        if (!kullanici.AktifMi)
        {
            ModelState.AddModelError("", "Hesabınız devre dışı bırakılmıştır.");
            return View(model);
        }

        var roller = await _db.KullaniciRoller
            .Include(kr => kr.Rol)
            .Where(kr => kr.KullaniciId == kullanici.Id)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, kullanici.KullaniciAdi),
            new("kullanici_id", kullanici.Id.ToString())
        };

        if (roller.Count > 0)
            claims.Add(new Claim("rol_ids", string.Join(",", roller.Select(r => r.RolId))));

        if (roller.Any(r => r.Rol.IsAdmin))
            claims.Add(new Claim("is_admin", "true"));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult Yetkisiz()
    {
        ViewData["Title"] = "Erişim Reddedildi";
        return View();
    }
}
