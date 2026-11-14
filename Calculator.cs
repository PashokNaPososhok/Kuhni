using Kuhni.Models;
namespace Kuhni;

public static class Calculator
{
    public static Configuration ForKitchen(Kitchen kitchen, Pricing pricing)
    {
        return Normalize(kitchen.Config, pricing, false);
    }
    public static Configuration Normalize(Configuration config, Pricing pricing, bool strict = true)
    {
        if (!new[] { "straight", "l", "u" }.Contains(config.Shape)) throw new ArgumentException("Выберите форму кухни.");
        if (config.Length < 0.6m || config.Length > 8 || config.Width < 0.6m || config.Width > 8 || config.ReturnLength < 0.6m || config.ReturnLength > 8 || config.Height < 200 || config.Height > 300 || config.Depth < 45 || config.Depth > 90)
            throw new ArgumentException("Длина и ширина: 0,6–8 м; высота: 200–300 см; глубина: 45–90 см.");
        config.Length = Math.Round(config.Length, 2, MidpointRounding.AwayFromZero);
        config.Width = Math.Round(config.Width, 2, MidpointRounding.AwayFromZero);
        config.ReturnLength = Math.Round(config.ReturnLength, 2, MidpointRounding.AwayFromZero);
        Dictionary<string, string> materials = new();
        foreach (var category in pricing.Categories)
        {
            string selected = config.Materials?.GetValueOrDefault(category.Key) ?? "";
            if (selected != "" && !category.Value.Options.ContainsKey(selected))
            {
                if (strict) throw new ArgumentException("Расценки изменились. Обновите страницу и выберите материал заново.");
                selected = "";
            }
            materials[category.Key] = selected;
        }
        config.Materials = materials;
        Dictionary<string, int> extras = new Dictionary<string, int>();
        foreach (var item in pricing.Extras)
        {
            int count = config.Extras?.GetValueOrDefault(item.Key) ?? 0;
            if (count < 0 || count > 1) throw new ArgumentException("Проверьте количество дополнительных опций.");
            extras[item.Key] = count;
        }
        config.Extras = extras;
        return config;
    }
    private static void AddLine(Quote quote, string label, decimal amount)
    {
        long rubles = (long)Math.Round(amount, 0, MidpointRounding.AwayFromZero);
        quote.Lines.Add(new QuoteLine { Label = label, Amount = rubles });
    }

    public static Quote Calculate(Configuration config, Pricing pricing, decimal surcharge = 0)
    {
        config = Normalize(config, pricing);
        decimal length = config.Length;
        if (config.Shape != "straight") length += config.Width - 0.6m;
        if (config.Shape == "u") length += config.ReturnLength - 0.6m;

        Quote quote = new Quote
        {
            Configuration = config,
            Length = length,
            ShapeLabel = Site.Shape(config.Shape)
        };
        decimal cabinetSize = length * config.Height;
        if (!config.Upper) cabinetSize *= 0.65m;

        foreach (var category in pricing.Categories)
        {
            string selected = config.Materials[category.Key];
            if (!category.Value.Options.TryGetValue(selected, out var option)) continue;
            decimal amount = option.Price;
            if (category.Value.Unit == "cabinet") amount = amount * cabinetSize / 240m;
            else if (category.Value.Unit == "countertop") amount = amount * length * config.Depth / 60m;
            else if (category.Value.Unit == "meter") amount *= length;
            AddLine(quote, category.Value.Name + " · " + option.Label, amount);
        }

        foreach (var item in pricing.Extras)
        {
            int count = config.Extras[item.Key];
            if (count == 0) continue;
            decimal amount = item.Value.Price * count;
            if (item.Value.Unit == "meter") amount *= length;
            string label = item.Value.Label;
            AddLine(quote, label, amount);
        }
        if (surcharge > 0) AddLine(quote, "Особенности модели", surcharge);
        long subtotal = quote.Lines.Sum(x => x.Amount);
        if (config.Installation)
            AddLine(quote, "Монтаж · " + Site.Number(pricing.Installation) + "%", subtotal * pricing.Installation / 100m);
        if (config.Delivery) AddLine(quote, "Доставка", pricing.Delivery);
        quote.Total = quote.Lines.Sum(x => x.Amount);
        return quote;
    }
    public static long Price(Kitchen kitchen, Pricing pricing) => Calculate(ForKitchen(kitchen, pricing), pricing, kitchen.Surcharge).Total;

    public static RequestForm Form(Kitchen? kitchen, Pricing pricing, string returnTo = "/calculator")
    {
        Configuration config = kitchen == null ? Normalize(new Configuration(), pricing, false) : ForKitchen(kitchen, pricing);
        return new RequestForm { HasCalculation = true, KitchenId = kitchen?.Id, Model = kitchen, Config = config, Quote = Calculate(config, pricing, kitchen?.Surcharge ?? 0), ReturnTo = returnTo };
    }
}
