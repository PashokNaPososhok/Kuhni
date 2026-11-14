using Kuhni.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.ComponentModel.DataAnnotations;
namespace Kuhni.Controllers;

[Authorize, Route("admin")]
public class AdminController : Controller
{
    private readonly Data db;
    private readonly IWebHostEnvironment environment;
    private readonly PasswordHasher<Admin> hasher = new PasswordHasher<Admin>();
    public AdminController(Data data, IWebHostEnvironment env) { db = data; environment = env; }
    [HttpGet("login"), AllowAnonymous]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true) return Redirect("/admin");
        ViewData["Title"] = "Вход администратора";
        return View("Index", new AdminPage { LoginPage = true });
    }
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(string login, string password)
    {
        if (string.IsNullOrWhiteSpace(login) || login.Length > 80 || string.IsNullOrEmpty(password) || password.Length > 200) ModelState.AddModelError("Login", "Укажите логин и пароль.");
        Admin? admin = ModelState.IsValid ? db.AdminByLogin(login) : null;
        if (admin == null || hasher.VerifyHashedPassword(admin, admin.Password, password ?? "") == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError("Login", "Неверный логин или пароль.");
            ViewData["Title"] = "Вход администратора";
            return View("Index", new AdminPage { LoginPage = true, Login = login ?? "" });
        }
        await SignIn(admin);
        return Redirect("/admin");
    }
    private async Task SignIn(Admin admin)
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()), new Claim(ClaimTypes.Name, admin.Login) }, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }
    [HttpPost("logout")]
    public async Task<IActionResult> Logout() { await HttpContext.SignOutAsync(); return Redirect("/admin/login"); }
    [HttpGet("")]
    public IActionResult Index(string section = "dashboard", string? edit = null, int? view = null, string group = "company", string? filter = null, [FromQuery] RequestList? requests = null)
    {
        if (string.IsNullOrEmpty(section) || !Site.Sections.ContainsKey(section)) section = "dashboard";
        AdminPage page = new AdminPage { Section = section, Group = group, Editing = edit != null, OnlyPending = filter == "pending" };
        page.Pending = db.NewRequestCount;
        page.Moderation = db.PendingReviewCount;
        if (section == "dashboard")
        {
            page.Counts = new Dictionary<string, int> { ["Всего заявок"] = db.RequestCount, ["Новые заявки"] = page.Pending, ["Моделей кухонь"] = db.KitchenCount, ["Отзывы на проверке"] = page.Moderation };
            page.Requests = db.Requests(new RequestList()); page.Requests.Rows = page.Requests.Rows.Take(8).ToList();
        }
        if (section == "requests")
        {
            if (view.HasValue)
            {
                page.Inquiry = db.Inquiry(view.Value);
                if (page.Inquiry == null) return NotFound();
                page.Snapshot = page.Inquiry.Snapshot;
            }
            else
            {
                requests ??= new RequestList();
                if (!ModelState.IsValid || requests.Q?.Length > 100 || (!string.IsNullOrEmpty(requests.Status) && !new[] { "new", "work", "done" }.Contains(requests.Status))) return BadRequest("Проверьте фильтры заявок.");
                page.Requests = db.Requests(requests);
            }
        }
        int id = 0;
        if (edit != null && edit != "new" && (!int.TryParse(edit, out id) || id < 1)) return BadRequest();
        if (section == "catalog")
        {
            page.Kitchens = db.Kitchens();
            if (id > 0) { var kitchen = db.Kitchen(id); if (kitchen == null) return NotFound(); page.Kitchen = kitchen; }
            page.Config = page.Kitchen.Id > 0 ? Calculator.ForKitchen(page.Kitchen, db.Pricing) : new Configuration();
        }
        if (section == "portfolio")
        {
            page.Kitchens = db.Kitchens(); page.Projects = db.Projects();
            if (id > 0) { var project = db.Project(id); if (project == null) return NotFound(); page.Project = project; }
        }
        if (section == "reviews")
        {
            page.Reviews = db.Reviews(true, page.OnlyPending);
            if (id > 0) { var review = db.Review(id); if (review == null) return NotFound(); page.Review = review; }
        }
        if (section == "content" && (string.IsNullOrEmpty(group) || !db.Groups.ContainsKey(group))) page.Group = "company";
        page.Login = User.Identity?.Name ?? "";
        ViewData["Title"] = Site.Sections[section];
        return View(page);
    }
    private IActionResult Failed(string section, int id = 0, string? error = null)
    {
        TempData["Error"] = error ?? string.Join(" ", ModelState.Values.SelectMany(x => x.Errors).Select(x => string.IsNullOrEmpty(x.ErrorMessage) ? "Проверьте заполненные поля." : x.ErrorMessage));
        return Redirect("/admin?section=" + section + (id > 0 ? "&edit=" + id : ""));
    }
    private IActionResult Saved(string section, string message = "Изменения сохранены.") { TempData["Success"] = message; return Redirect("/admin?section=" + section); }
    [HttpPost("save-catalog")]
    public async Task<IActionResult> SaveKitchen([Bind("Id,Name,Style,Description,Image,Gallery,Surcharge,SortOrder")] Kitchen kitchen, Configuration config)
    {
        if (kitchen.Id > 0 && db.Kitchen(kitchen.Id) == null) return NotFound();
        if (!ModelState.IsValid) return Failed("catalog", kitchen.Id);
        try
        {
            kitchen.Config = Calculator.Normalize(config, db.Pricing);
            kitchen.Image = ImagePath(await UploadOne("main_photo") ?? kitchen.Image);
            kitchen.Gallery = await Gallery(kitchen.Gallery, "gallery_photos");
            db.SaveKitchen(kitchen);
        }
        catch (ArgumentException e) { return Failed("catalog", kitchen.Id, e.Message); }
        return Saved("catalog", "Модель сохранена.");
    }
    [HttpPost("save-portfolio")]
    public async Task<IActionResult> SaveProject([Bind("Id,KitchenId,Name,Summary,Description,Image,Gallery,Area,City,Duration,SortOrder")] Project project)
    {
        if (project.Id > 0 && db.Project(project.Id) == null) return NotFound();
        if (!ModelState.IsValid) return Failed("portfolio", project.Id);
        if (project.KitchenId == 0) project.KitchenId = null;
        if (project.KitchenId.HasValue && db.Kitchen(project.KitchenId.Value) == null) return Failed("portfolio", project.Id, "Связанная модель не найдена.");
        try
        {
            project.Image = ImagePath(await UploadOne("main_photo") ?? project.Image);
            project.Gallery = await Gallery(project.Gallery, "gallery_photos");
            project.Summary ??= ""; project.City ??= ""; project.Area ??= ""; project.Duration ??= "";
            db.SaveProject(project);
        }
        catch (ArgumentException e) { return Failed("portfolio", project.Id, e.Message); }
        return Saved("portfolio", "Проект сохранён.");
    }
    [HttpPost("save-review")]
    public IActionResult SaveReview([Bind("Id,Name,Rating,Text,Approved,SortOrder")] Review review)
    {
        if (review.Id > 0 && db.Review(review.Id) == null) return NotFound();
        if (!ModelState.IsValid) return Failed("reviews", review.Id);
        db.SaveReview(review); return Saved("reviews", "Отзыв сохранён.");
    }
    [HttpPost("approve")]
    public IActionResult Approve(int id) { db.ApproveReview(id); return Saved("reviews", "Отзыв опубликован."); }
    [HttpPost("delete")]
    public IActionResult Delete(string section, int id)
    {
        if (!new[] { "catalog", "portfolio", "reviews", "requests" }.Contains(section) || id < 1) return BadRequest();
        db.Delete(section, id); return Saved(section, "Запись удалена.");
    }
    [HttpPost("save-request")]
    public IActionResult SaveRequest(int id, string status, string? note)
    {
        if (db.Inquiry(id) == null) return NotFound();
        if (!new[] { "new", "work", "done" }.Contains(status) || note?.Length > 5000) return BadRequest();
        db.SaveRequest(id, status, note ?? "");
        TempData["Success"] = "Заявка обновлена.";
        return Redirect("/admin?section=requests&view=" + id);
    }
    [HttpPost("save-content")]
    public async Task<IActionResult> SaveContent(string group, Dictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(group) || !db.Groups.ContainsKey(group)) return BadRequest();
        Dictionary<string, string> updates = new Dictionary<string, string>();
        try
        {
            foreach (var field in db.Groups[group].Fields)
            {
                string value = values.GetValueOrDefault(field.Key) ?? "";
                if (value.Length > 12000) throw new ArgumentException("Слишком длинное поле: " + field.Value);
                if (field.Key.EndsWith("_image")) value = ImagePath(await UploadOne(field.Key + "_upload") ?? value);
                if (new[] { "brand", "hero_title", "about_title", "phone", "address", "developer" }.Contains(field.Key) && string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Заполните поле «" + field.Value + "».");
                if (new[] { "vk_url", "telegram_url" }.Contains(field.Key) && value != "" && !ValidUrl(value)) throw new ArgumentException("Укажите полную HTTPS-ссылку.");
                if (field.Key == "map_url" && (!ValidUrl(value) || !new[] { "maps.google.com", "www.google.com", "yandex.ru", "yandex.com" }.Contains(new Uri(value).Host))) throw new ArgumentException("Укажите ссылку для встраивания карты Google или Яндекс.");
                if (field.Key == "email" && !new EmailAddressAttribute().IsValid(value)) throw new ArgumentException("Укажите корректную электронную почту.");
                updates[field.Key] = value;
            }
            db.SaveSettings(updates);
        }
        catch (ArgumentException e) { TempData["Error"] = e.Message; return Redirect("/admin?section=content&group=" + group); }
        TempData["Success"] = "Содержимое обновлено на всех связанных страницах.";
        return Redirect("/admin?section=content&group=" + group);
    }
    [HttpPost("save-pricing")]
    public IActionResult SavePricing(Pricing pricing)
    {
        if (!ModelState.IsValid) return Failed("pricing", error: "Проверьте расценки.");
        Pricing current = db.Pricing;
        try
        {
            foreach (var category in current.Categories)
            {
                if (!pricing.Categories.TryGetValue(category.Key, out var input) || !ValidName(input.Name))
                    throw new ArgumentException("Проверьте названия категорий и обновите страницу, если расценки менялись.");
                category.Value.Name = input.Name.Trim();
                foreach (var option in category.Value.Options)
                {
                    if (!input.Options.TryGetValue(option.Key, out var value)) throw new ArgumentException("Проверьте варианты категории.");
                    UpdatePrice(option.Value, value);
                }
            }
            foreach (var extra in current.Extras)
            {
                if (!pricing.Extras.TryGetValue(extra.Key, out var value)) throw new ArgumentException("Проверьте дополнительные опции.");
                UpdatePrice(extra.Value, value);
            }
            if (pricing.Delivery < 0 || pricing.Delivery > 100000 || pricing.Installation < 0 || pricing.Installation > 50) throw new ArgumentException("Проверьте цену доставки и процент монтажа.");
            current.Delivery = Math.Round(pricing.Delivery, 2, MidpointRounding.AwayFromZero); current.Installation = Math.Round(pricing.Installation, 2, MidpointRounding.AwayFromZero);
            db.SavePricing(current);
        }
        catch (ArgumentException e) { return Failed("pricing", error: e.Message); }
        return Saved("pricing", "Расценки обновлены. Старые заявки сохраняют свою смету.");
    }
    private static bool ValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 100;
    private static void UpdatePrice(PriceOption current, PriceOption input)
    {
        if (!ValidName(input.Label) || input.Price < 0 || input.Price > 1000000)
            throw new ArgumentException("Укажите название и цену от 0 до 1 000 000 ₽.");
        current.Label = input.Label.Trim();
        current.Price = Math.Round(input.Price, 2, MidpointRounding.AwayFromZero);
    }
    [HttpPost("add-category")]
    public IActionResult AddCategory(string name, string unit)
    {
        if (!ValidName(name) || !new[] { "meter", "fixed" }.Contains(unit))
            return Failed("pricing", error: "Укажите название и способ расчёта категории.");
        if (db.Pricing.Categories.Values.Any(x => x.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)))
            return Failed("pricing", error: "Категория с таким названием уже есть.");
        db.AddCategory(name.Trim(), unit);
        return Saved("pricing", "Категория добавлена. В существующих кухнях она останется пустой до редактирования.");
    }
    [HttpPost("delete-category")]
    public IActionResult DeleteCategory(string id)
    {
        if (string.IsNullOrEmpty(id) || !db.Pricing.Categories.ContainsKey(id)) return NotFound();
        db.DeleteCategory(id);
        return Saved("pricing", "Категория удалена. Старые заявки сохраняют свою смету.");
    }
    [HttpPost("add-option")]
    public IActionResult AddOption(string category, string name, decimal price)
    {
        if (string.IsNullOrEmpty(category) || !db.Pricing.Categories.ContainsKey(category)) return NotFound();
        if (!ModelState.IsValid || !ValidName(name) || price < 0 || price > 1000000)
            return Failed("pricing", error: "Укажите название варианта и цену от 0 до 1 000 000 ₽.");
        db.AddOption(category, name.Trim(), Math.Round(price, 2, MidpointRounding.AwayFromZero));
        return Saved("pricing", "Вариант добавлен. Выбрать его для кухни можно в каталоге.");
    }
    [HttpPost("delete-option")]
    public IActionResult DeleteOption(string category, string id)
    {
        if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(id) || !db.Pricing.Categories.TryGetValue(category, out var group) || !group.Options.ContainsKey(id)) return NotFound();
        db.DeleteOption(category, id);
        return Saved("pricing", "Вариант удалён. В кухнях, где он был выбран, поле теперь пустое.");
    }
    [HttpPost("save-account")]
    public async Task<IActionResult> SaveAccount(string login, string currentPassword, string password)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id)) return Forbid();
        Admin? admin = db.AdminById(id);
        if (admin == null) return Forbid();
        if (string.IsNullOrEmpty(currentPassword) || hasher.VerifyHashedPassword(admin, admin.Password, currentPassword) == PasswordVerificationResult.Failed) return Failed("account", error: "Текущий пароль неверен.");
        if (string.IsNullOrEmpty(login) || !Regex.IsMatch(login, @"^[A-Za-z0-9_.-]{3,80}$") || string.IsNullOrEmpty(password) || password.Length < 10 || password.Length > 200) return Failed("account", error: "Логин: от 3 латинских символов. Пароль: от 10 символов.");
        Admin? duplicate = db.AdminByLogin(login);
        if (duplicate != null && duplicate.Id != admin.Id) return Failed("account", error: "Логин уже занят.");
        admin.Login = login; admin.Password = hasher.HashPassword(admin, password); db.SaveAdmin(admin);
        await SignIn(admin);
        return Saved("account", "Логин и пароль изменены.");
    }
    private static bool ValidUrl(string value) => value.Length <= 500 && Uri.TryCreate(value, UriKind.Absolute, out var url) && url.Scheme == "https";
    private static string ImagePath(string? value)
    {
        value = (value ?? "").Trim();
        if (Regex.IsMatch(value, @"^(assets/(img|images)|media)/[a-zA-Z0-9_.-]+\.(jpg|jpeg|png|webp)$", RegexOptions.IgnoreCase) || ValidUrl(value)) return value;
        throw new ArgumentException("Фотография: используйте assets/img/…, media/… или HTTPS-ссылку.");
    }
    private async Task<string?> UploadOne(string name)
    {
        IFormFile? file = Request.Form.Files.GetFile(name);
        return file == null || file.Length == 0 ? null : await SavePhoto(file);
    }
    private async Task<string> Gallery(string? value, string name)
    {
        List<string> paths = (value ?? "").Split('\n').Select(x => x.Trim()).Where(x => x != "").Select(ImagePath).ToList();
        var files = Request.Form.Files.Where(x => x.Name == name && x.Length > 0).ToList();
        if (files.Count > 12) throw new ArgumentException("За один раз можно загрузить до 12 фото.");
        foreach (var file in files) paths.Add(await SavePhoto(file));
        string result = string.Join("\n", paths.Distinct());
        if (result.Length > 10000) throw new ArgumentException("В галерее слишком много ссылок.");
        return result;
    }
    private async Task<string> SavePhoto(IFormFile file)
    {
        if (file.Length > 5 * 1024 * 1024) throw new ArgumentException("Максимальный размер фото — 5 МБ.");
        using var stream = new MemoryStream(); await file.CopyToAsync(stream); byte[] bytes = stream.ToArray();
        string extension;
        if (bytes.Length > 12 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255) extension = "jpg";
        else if (bytes.Length > 24 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) extension = "png";
        else if (bytes.Length > 12 && System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP") extension = "webp";
        else throw new ArgumentException("Загрузите фотографию в формате JPG, PNG или WebP.");
        string folder = Path.Combine(environment.ContentRootPath, "AppData/uploads"); Directory.CreateDirectory(folder);
        string filename = Guid.NewGuid().ToString("N") + "." + extension;
        await System.IO.File.WriteAllBytesAsync(Path.Combine(folder, filename), bytes);
        return "media/" + filename;
    }
}
