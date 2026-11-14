using Kuhni.Models;
using System.Text.Json;

namespace Kuhni;

public class Data
{
    private static readonly object fileLock = new();
    private readonly string path;
    private readonly string fieldsPath;
    private SiteData data;

    public Data(IWebHostEnvironment environment)
    {
        string folder = Path.Combine(environment.ContentRootPath, "AppData");
        Directory.CreateDirectory(folder);
        path = Path.Combine(folder, "site.json");
        fieldsPath = Path.Combine(environment.ContentRootPath, "Data/fields.json");

        lock (fileLock)
        {
            if (!File.Exists(path))
                File.Copy(Path.Combine(environment.ContentRootPath, "Data/start.json"), path);
            Upgrade.Run(path);
            data = Read();
        }
    }

    private SiteData Read()
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SiteData>(json, Site.Json)
            ?? throw new InvalidDataException("Не удалось прочитать AppData/site.json.");
    }

    private void Save(Action<SiteData> change)
    {
        lock (fileLock)
        {
            // Берём свежие данные, чтобы не потерять одновременные заявки.
            SiteData latest = Read();
            change(latest);
            string json = JsonSerializer.Serialize(latest, Site.Json);
            // Основной файл заменяем только после успешной записи.
            File.WriteAllText(path + ".tmp", json);
            File.Move(path + ".tmp", path, true);
            data = latest;
        }
    }

    public string Setting(string name) => data.Settings.GetValueOrDefault(name, "");
    public Pricing Pricing => data.Pricing;
    public int RequestCount => data.Requests.Count;
    public int KitchenCount => data.Kitchens.Count;
    public int NewRequestCount => data.Requests.Count(x => x.Status == "new");
    public int PendingReviewCount => data.Reviews.Count(x => !x.Approved);
    public Dictionary<string, ContentGroup> Groups =>
        JsonSerializer.Deserialize<Dictionary<string, ContentGroup>>(File.ReadAllText(fieldsPath), Site.Json)!;

    public void SaveSettings(Dictionary<string, string> values)
    {
        Save(site =>
        {
            foreach (var item in values) site.Settings[item.Key] = item.Value;
        });
    }

    public void SavePricing(Pricing pricing)
    {
        Save(site =>
        {
            foreach (var category in pricing.Categories)
            {
                if (!site.Pricing.Categories.TryGetValue(category.Key, out var current)) continue;
                current.Name = category.Value.Name;
                foreach (var option in category.Value.Options)
                    if (current.Options.ContainsKey(option.Key)) current.Options[option.Key] = option.Value;
            }
            foreach (var extra in pricing.Extras)
                if (site.Pricing.Extras.ContainsKey(extra.Key)) site.Pricing.Extras[extra.Key] = extra.Value;
            site.Pricing.Delivery = pricing.Delivery;
            site.Pricing.Installation = pricing.Installation;
        });
    }

    public void AddCategory(string name, string unit)
    {
        string id = "cat_" + Guid.NewGuid().ToString("N");
        Save(site => site.Pricing.Categories[id] = new PriceCategory { Name = name, Unit = unit });
    }

    public void DeleteCategory(string id)
    {
        Save(site =>
        {
            site.Pricing.Categories.Remove(id);
            foreach (Kitchen kitchen in site.Kitchens) kitchen.Config.Materials.Remove(id);
        });
    }

    public void AddOption(string category, string name, decimal price)
    {
        Save(site =>
        {
            if (!site.Pricing.Categories.TryGetValue(category, out var group)) return;
            group.Options["opt_" + Guid.NewGuid().ToString("N")] = new PriceOption { Label = name, Price = price };
        });
    }

    public void DeleteOption(string category, string id)
    {
        Save(site =>
        {
            if (!site.Pricing.Categories.TryGetValue(category, out var group)) return;
            group.Options.Remove(id);
            foreach (Kitchen kitchen in site.Kitchens)
                if (kitchen.Config.Materials.GetValueOrDefault(category) == id) kitchen.Config.Materials.Remove(category);
        });
    }

    public List<Kitchen> Kitchens()
    {
        return data.Kitchens.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToList();
    }

    public Kitchen? Kitchen(int id) => Kitchens().Find(x => x.Id == id);

    public void SaveKitchen(Kitchen kitchen)
    {
        Save(site =>
        {
            if (kitchen.Id == 0) kitchen.Id = site.NextKitchenId++;
            site.Kitchens.RemoveAll(x => x.Id == kitchen.Id);
            site.Kitchens.Add(kitchen);
        });
    }

    public List<Project> Projects(int? kitchenId = null)
    {
        var projects = data.Projects.Where(x => !kitchenId.HasValue || x.KitchenId == kitchenId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToList();
        foreach (Project project in projects)
            project.KitchenName = data.Kitchens.Find(x => x.Id == project.KitchenId)?.Name;
        return projects;
    }

    public Project? Project(int id) => Projects().Find(x => x.Id == id);

    public void SaveProject(Project project)
    {
        Save(site =>
        {
            if (project.Id == 0) project.Id = site.NextProjectId++;
            site.Projects.RemoveAll(x => x.Id == project.Id);
            site.Projects.Add(project);
        });
    }

    public List<Review> Reviews(bool all = false, bool pending = false)
    {
        return data.Reviews.Where(x => (all || x.Approved) && (!pending || !x.Approved))
            .OrderBy(x => x.SortOrder).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToList();
    }

    public Review? Review(int id) => data.Reviews.Find(x => x.Id == id);

    public void SaveReview(Review review)
    {
        Save(site =>
        {
            if (review.Id == 0)
            {
                review.Id = site.NextReviewId++;
                review.CreatedAt = DateTime.Now;
            }
            else review.CreatedAt = site.Reviews.Find(x => x.Id == review.Id)?.CreatedAt ?? DateTime.Now;
            site.Reviews.RemoveAll(x => x.Id == review.Id);
            site.Reviews.Add(review);
        });
    }

    public void ApproveReview(int id)
    {
        Save(site =>
        {
            Review? review = site.Reviews.Find(x => x.Id == id);
            if (review != null) review.Approved = true;
        });
    }

    public int AddRequest(Inquiry inquiry)
    {
        Save(site =>
        {
            inquiry.Id = site.NextRequestId++;
            inquiry.CreatedAt = DateTime.Now;
            inquiry.UpdatedAt = inquiry.CreatedAt;
            site.Requests.Add(inquiry);
        });
        return inquiry.Id;
    }

    public Inquiry? Inquiry(int id) => data.Requests.Find(x => x.Id == id);

    public void SaveRequest(int id, string status, string note)
    {
        Save(site =>
        {
            Inquiry? inquiry = site.Requests.Find(x => x.Id == id);
            if (inquiry == null) return;
            inquiry.Status = status;
            inquiry.Note = note;
            inquiry.UpdatedAt = DateTime.Now;
        });
    }

    public RequestList Requests(RequestList list)
    {
        var rows = data.Requests.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(list.Q))
            rows = rows.Where(x => (x.Name + " " + x.Phone + " " + x.Email).Contains(list.Q, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(list.Status)) rows = rows.Where(x => x.Status == list.Status);
        rows = list.Sort switch
        {
            "oldest" => rows.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            "sum_desc" => rows.OrderByDescending(x => x.Total).ThenByDescending(x => x.Id),
            "sum_asc" => rows.OrderBy(x => x.Total).ThenBy(x => x.Id),
            "name" => rows.OrderBy(x => x.Name),
            _ => rows.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
        };
        list.Count = rows.Count();
        list.Pages = Math.Max(1, (int)Math.Ceiling(list.Count / 20m));
        list.P = Math.Clamp(list.P, 1, list.Pages);
        list.Rows = rows.Skip((list.P - 1) * 20).Take(20).ToList();
        return list;
    }

    public Admin? AdminByLogin(string login) =>
        string.Equals(data.Admin?.Login, login, StringComparison.OrdinalIgnoreCase) ? data.Admin : null;
    public Admin? AdminById(int id) => data.Admin?.Id == id ? data.Admin : null;
    public bool HasAdmin => data.Admin != null;
    public void SaveAdmin(Admin admin)
    {
        admin.Id = 1;
        Save(site => site.Admin = admin);
    }

    public void Delete(string section, int id)
    {
        Save(site =>
        {
            if (section == "catalog")
            {
                site.Kitchens.RemoveAll(x => x.Id == id);
                foreach (Project project in site.Projects.Where(x => x.KitchenId == id)) project.KitchenId = null;
                foreach (Inquiry inquiry in site.Requests.Where(x => x.KitchenId == id)) inquiry.KitchenId = null;
            }
            else if (section == "portfolio") site.Projects.RemoveAll(x => x.Id == id);
            else if (section == "reviews") site.Reviews.RemoveAll(x => x.Id == id);
            else if (section == "requests") site.Requests.RemoveAll(x => x.Id == id);
        });
    }
}
