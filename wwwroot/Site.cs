using System.Globalization;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Kuhni.Models;
namespace Kuhni;

public static class Site
{
    public static readonly JsonSerializerOptions Json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true, Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Cyrillic) };
    public static readonly Dictionary<string, string> Sections = new Dictionary<string, string>
    {
        ["dashboard"] = "Обзор",
        ["requests"] = "Заявки",
        ["catalog"] = "Каталог",
        ["portfolio"] = "Портфолио",
        ["reviews"] = "Отзывы",
        ["content"] = "Тексты и контакты",
        ["pricing"] = "Расценки",
        ["account"] = "Данные входа"
    };
    public static string Unit(string unit) => unit == "fixed" ? "за комплект" : "за погонный метр";
    public static string MaterialText(Configuration config, Pricing pricing)
    {
        List<string> names = new();
        foreach (var item in pricing.Categories)
        {
            string selected = config.Materials.GetValueOrDefault(item.Key, "");
            if (item.Value.Options.TryGetValue(selected, out var option)) names.Add(option.Label);
        }
        return string.Join(" · ", names);
    }
    public static string Money(decimal? amount) => (amount ?? 0).ToString("N0", CultureInfo.GetCultureInfo("ru-RU")) + " ₽";
    public static string Number(decimal? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "";
    public static string Shape(string shape) => shape switch { "l" => "Угловая", "u" => "П-образная", _ => "Прямая" };
    public static string Status(string status) => status switch { "work" => "В работе", "done" => "Обработана", _ => "Новая" };
    public static string Image(string? path)
    {
        path = path ?? "assets/img/kitchen-1.jpg";
        if (path.StartsWith("https://")) return path;
        return "/" + path.Replace("assets/images/", "assets/img/").TrimStart('/');
    }
    public static List<string> Gallery(string image, string? gallery)
    {
        return new[] { image }.Concat((gallery ?? "").Split('\n')).Select(x => x.Trim()).Where(x => x != "").Distinct().ToList();
    }
    public static string Lines(string text) => HtmlEncoder.Default.Encode(text ?? "").Replace("\r\n", "\n").Replace("\n", "<br>");
}
