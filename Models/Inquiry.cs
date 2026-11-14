namespace Kuhni.Models;

public class Inquiry
{
    public int Id { get; set; }
    public int? KitchenId { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Message { get; set; } = "";
    public Quote? Snapshot { get; set; }
    public decimal? Total { get; set; }
    public string Status { get; set; } = "new";
    public string Source { get; set; } = "contacts";
    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
public class Admin
{
    public int Id { get; set; }
    public string Login { get; set; } = "";
    public string Password { get; set; } = "";
}
