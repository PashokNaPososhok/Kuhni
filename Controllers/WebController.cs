using Kuhni.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.RegularExpressions;
namespace Kuhni.Controllers;

public class WebController : Controller
{
    private readonly Data db;
    private readonly IWebHostEnvironment environment;
    public WebController(Data data, IWebHostEnvironment env) { db = data; environment = env; }
    [HttpGet("/catalog")]
    public IActionResult Catalog(CatalogPage page)
    {
        if (!ModelState.IsValid) return BadRequest("Проверьте фильтры каталога.");
        var kitchens = db.Kitchens().AsEnumerable();
        page.Styles = kitchens.Select(x => x.Style).Distinct().OrderBy(x => x).ToList();
        if (!string.IsNullOrWhiteSpace(page.Q)) kitchens = kitchens.Where(x => (x.Name + " " + x.Style + " " + x.Description).Contains(page.Q, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(page.Style)) kitchens = kitchens.Where(x => x.Style == page.Style);
        if (page.Min.HasValue) kitchens = kitchens.Where(x => Calculator.Price(x, db.Pricing) >= page.Min);
        if (page.Max.HasValue) kitchens = kitchens.Where(x => Calculator.Price(x, db.Pricing) <= page.Max);
        if (page.Length.HasValue) kitchens = kitchens.Where(x => Calculator.ForKitchen(x, db.Pricing).Length <= page.Length);
        kitchens = page.Sort switch
        {
            "price_asc" => kitchens.OrderBy(x => Calculator.Price(x, db.Pricing)),
            "price_desc" => kitchens.OrderByDescending(x => Calculator.Price(x, db.Pricing)),
            "name" => kitchens.OrderBy(x => x.Name),
            _ => kitchens
        };
        page.Kitchens = kitchens.ToList();
        ViewData["Title"] = db.Setting("catalog_title");
        return View(page);
    }
    [HttpGet("/kitchen")]
    public IActionResult Kitchen(int id)
    {
        Kitchen? kitchen = db.Kitchen(id);
        if (kitchen == null) return NotFound();
        ViewBag.Projects = db.Projects(kitchenId: id);
        ViewData["Title"] = kitchen.Name;
        return View(kitchen);
    }
    [HttpGet("/portfolio")]
    public IActionResult Portfolio() { ViewData["Title"] = db.Setting("portfolio_title"); return View(db.Projects()); }
    [HttpGet("/project")]
    public IActionResult Project(int id)
    {
        Project? project = db.Project(id);
        if (project == null) return NotFound();
        ViewBag.Kitchen = project.KitchenId.HasValue ? db.Kitchen(project.KitchenId.Value) : null;
        ViewData["Title"] = project.Name;
        return View(project);
    }
    [HttpGet("/about")]
    public IActionResult About() { ViewData["Title"] = "О компании"; return View(); }
    [HttpGet("/contact"), HttpGet("/contacts")]
    public IActionResult Contact() { ViewData["Title"] = db.Setting("contacts_title"); return View(new RequestForm()); }
    [HttpGet("/reviews")]
    public IActionResult Reviews() { ViewData["Title"] = db.Setting("reviews_title"); return View(db.Reviews()); }
    [HttpGet("/calculator")]
    public IActionResult Calculate(int? model)
    {
        Kitchen? kitchen = model.HasValue ? db.Kitchen(model.Value) : null;
        ViewData["Title"] = db.Setting("calculator_title");
        return View("Calculator", Calculator.Form(kitchen, db.Pricing));
    }
    [HttpPost("/actions/request"), EnableRateLimiting("request")]
    public IActionResult AddRequest([Bind("Name,Phone,Email,Message,Consent,Website,ReturnTo,HasCalculation,KitchenId,Config")] RequestForm form)
    {
        form.Name = (form.Name ?? "").Trim(); form.Phone = (form.Phone ?? "").Trim(); form.Email = (form.Email ?? "").Trim(); form.Message = (form.Message ?? "").Trim();
        int digits = Regex.Replace(form.Phone, @"\D", "").Length;
        if (digits < 10 || digits > 15) ModelState.AddModelError("Phone", "Укажите телефон длиной от 10 до 15 цифр.");
        if (!form.Consent) ModelState.AddModelError("Consent", "Подтвердите согласие на обработку данных.");
        if (!string.IsNullOrEmpty(form.Website)) ModelState.AddModelError("Website", "Не удалось отправить форму.");
        if (form.HasCalculation)
        {
            if (form.KitchenId > 0)
            {
                form.Model = db.Kitchen(form.KitchenId.Value);
                if (form.Model == null) ModelState.AddModelError("KitchenId", "Модель недоступна. Выберите другую кухню.");
            }
            try
            {
                form.Config = Calculator.Normalize(form.Config ?? new Configuration(), db.Pricing);
                form.Quote = Calculator.Calculate(form.Config, db.Pricing, form.Model?.Surcharge ?? 0);
                form.Quote.Model = form.Model?.Name ?? "Индивидуальная кухня";
                form.Quote.CreatedAt = DateTime.UtcNow;
            }
            catch (ArgumentException e) { ModelState.AddModelError("Config", e.Message); form.Config = Calculator.Normalize(new Configuration(), db.Pricing, false); form.Quote = Calculator.Calculate(form.Config, db.Pricing); }
        }
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = db.Setting(form.HasCalculation ? "calculator_title" : "contacts_title");
            return View(form.HasCalculation ? "Calculator" : "Contact", form);
        }
        Inquiry inquiry = new Inquiry { Name = form.Name, Phone = form.Phone, Email = form.Email, Message = form.Message, KitchenId = form.HasCalculation ? form.Model?.Id : null };
        if (form.HasCalculation)
        {
            inquiry.Snapshot = form.Quote;
            inquiry.Total = form.Quote!.Total; inquiry.Source = "calculator";
        }
        int id = db.AddRequest(inquiry);
        TempData["Success"] = "Заявка №" + id + " отправлена. Мы свяжемся с вами по указанному телефону.";
        return LocalRedirect(new[] { "/", "/home", "/contact", "/contacts", "/calculator" }.Contains(form.ReturnTo) ? form.ReturnTo : "/contact");
    }
    [HttpPost("/actions/review"), EnableRateLimiting("review")]
    public IActionResult AddReview([Bind("Name,Rating,Text")] Review review, string? website)
    {
        if (!string.IsNullOrEmpty(website)) ModelState.AddModelError("Website", "Не удалось отправить отзыв.");
        if (!ModelState.IsValid) { ViewData["Title"] = db.Setting("reviews_title"); return View("Reviews", db.Reviews()); }
        review.Id = 0; review.Approved = false;
        db.SaveReview(review);
        TempData["Success"] = "Спасибо за отзыв! Он появится после проверки администратором.";
        return Redirect("/reviews");
    }
    [HttpGet("/media/{filename}")]
    public IActionResult Media(string filename)
    {
        if (!Regex.IsMatch(filename, @"^[a-zA-Z0-9_-]+\.(jpg|jpeg|png|webp)$")) return NotFound();
        string path = Path.Combine(environment.ContentRootPath, "AppData/uploads", filename);
        if (!System.IO.File.Exists(path)) return NotFound();
        string type = Path.GetExtension(filename).ToLowerInvariant() switch { ".png" => "image/png", ".webp" => "image/webp", _ => "image/jpeg" };
        return PhysicalFile(path, type);
    }
    [Route("/error/{code:int=500}"), IgnoreAntiforgeryToken]
    public IActionResult Error(int code = 500)
    {
        Response.StatusCode = code;
        return View("Error", code);
    }
}
