using System.ComponentModel.DataAnnotations;
namespace Kuhni.Models;

public class Configuration
{
    public string Shape { get; set; } = "straight";
    [Range(typeof(decimal), "0.6", "8", ErrorMessage = "Длина: от 0,6 до 8 м.")]
    public decimal Length { get; set; } = 3;
    [Range(typeof(decimal), "0.6", "8", ErrorMessage = "Ширина: от 0,6 до 8 м.")]
    public decimal Width { get; set; } = 2;
    [Range(typeof(decimal), "0.6", "8", ErrorMessage = "Вторая длина: от 0,6 до 8 м.")]
    public decimal ReturnLength { get; set; } = 2;
    [Range(200, 300, ErrorMessage = "Высота: от 200 до 300 см.")]
    public int Height { get; set; } = 240;
    [Range(45, 90, ErrorMessage = "Глубина: от 45 до 90 см.")]
    public int Depth { get; set; } = 60;
    public Dictionary<string, string> Materials { get; set; } = new() { ["body"] = "ldsp", ["countertop"] = "laminate" };
    public bool Upper { get; set; } = true;
    public bool Delivery { get; set; } = true;
    public bool Installation { get; set; } = true;
    public Dictionary<string, int> Extras { get; set; } = new Dictionary<string, int> { ["lighting"] = 1 };
}
public class PriceOption
{
    public string Label { get; set; } = "";
    public decimal Price { get; set; }
    public string Unit { get; set; } = "";
}
public class PriceCategory
{
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "meter";
    public Dictionary<string, PriceOption> Options { get; set; } = new();
}
public class Pricing
{
    public Dictionary<string, PriceCategory> Categories { get; set; } = new();
    public Dictionary<string, PriceOption> Extras { get; set; } = new();
    public decimal Delivery { get; set; }
    public decimal Installation { get; set; }
}
public class QuoteLine
{
    public string Label { get; set; } = "";
    public long Amount { get; set; }
}
public class Quote
{
    public Configuration Configuration { get; set; } = new Configuration();
    public decimal Length { get; set; }
    public List<QuoteLine> Lines { get; set; } = new List<QuoteLine>();
    public long Total { get; set; }
    public string Model { get; set; } = "Индивидуальная кухня";
    public string ShapeLabel { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
