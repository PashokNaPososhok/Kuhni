using System.Text.Json.Nodes;

namespace Kuhni;

public static class Upgrade
{
    public static void Run(string path)
    {
        JsonObject site = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        if ((int?)site["version"] == 2) return;

        // Старый файл сохраняем отдельно вместе с паролем и заявками.
        string backup = path + ".before-v2.json";
        if (!File.Exists(backup)) File.Copy(path, backup);
        JsonObject pricing = site["pricing"]!.AsObject();
        pricing["categories"] = new JsonObject
        {
            ["body"] = new JsonObject { ["name"] = "Корпус", ["unit"] = "cabinet", ["options"] = pricing["body"]?.DeepClone() ?? new JsonObject() },
            ["countertop"] = new JsonObject { ["name"] = "Столешница", ["unit"] = "countertop", ["options"] = pricing["countertop"]?.DeepClone() ?? new JsonObject() }
        };
        foreach (string field in new[] { "body", "facade", "countertop", "hardware" }) pricing.Remove(field);
        pricing["extras"]?.AsObject().Remove("backsplash");
        pricing["extras"]?.AsObject().Remove("drawers");
        foreach (string list in new[] { "kitchens", "projects", "reviews" })
        {
            foreach (JsonNode? item in site[list]!.AsArray())
            {
                item!.AsObject().Remove("featured");
                item.AsObject().Remove("active");
                ConvertConfig(item["config"] as JsonObject);
            }
        }
        foreach (JsonNode? request in site["requests"]!.AsArray())
            ConvertConfig(request?["snapshot"]?["configuration"] as JsonObject);
        site["settings"]!["email"] = "mveu@mveu.ru";
        site["version"] = 2;
        File.WriteAllText(path + ".tmp", site.ToJsonString(Site.Json));
        File.Move(path + ".tmp", path, true);
    }

    private static void ConvertConfig(JsonObject? config)
    {
        if (config == null) return;
        config["length"] = config["a"]?.DeepClone() ?? JsonValue.Create(3);
        config["width"] = config["b"]?.DeepClone() ?? JsonValue.Create(2);
        config["returnLength"] = config["c"]?.DeepClone() ?? JsonValue.Create(2);
        config["materials"] = new JsonObject
        {
            ["body"] = config["body"]?.DeepClone() ?? JsonValue.Create(""),
            ["countertop"] = config["countertop"]?.DeepClone() ?? JsonValue.Create("")
        };
        config["extras"]?.AsObject().Remove("backsplash");
        config["extras"]?.AsObject().Remove("drawers");
        foreach (string field in new[] { "a", "b", "c", "body", "facade", "countertop", "hardware" }) config.Remove(field);
    }
}
