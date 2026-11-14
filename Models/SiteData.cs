namespace Kuhni.Models;

public class SiteData
{
    public int Version { get; set; } = 2;
    public Dictionary<string, string> Settings { get; set; } = new();
    public Pricing Pricing { get; set; } = new();
    public List<Kitchen> Kitchens { get; set; } = new();
    public List<Project> Projects { get; set; } = new();
    public List<Review> Reviews { get; set; } = new();
    public List<Inquiry> Requests { get; set; } = new();
    public Admin? Admin { get; set; }
    public int NextKitchenId { get; set; } = 1;
    public int NextProjectId { get; set; } = 1;
    public int NextReviewId { get; set; } = 1;
    public int NextRequestId { get; set; } = 1;
}
