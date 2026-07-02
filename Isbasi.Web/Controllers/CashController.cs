using Isbasi.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Isbasi.Web.Controllers;

[Route("cash")]
public class CashController : Controller
{
    private readonly AppDbContext _db;

    public CashController(AppDbContext db) => _db = db;

    [HttpGet("cashstatus")]
    public async Task<IActionResult> CashStatus()
    {
        ViewBag.Safes = await _db.Safes.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        ViewBag.Banks = await _db.BankAccounts.AsNoTracking().OrderBy(b => b.Name).ToListAsync();
        ViewBag.Balances = await CashBalanceCalculator.Compute(_db);
        return View();
    }
}
