using System.ComponentModel.DataAnnotations;
namespace Kuhni.Models;

public class RequestForm
{
    [Required(ErrorMessage = "Укажите имя."), StringLength(80, MinimumLength = 2, ErrorMessage = "Имя: от 2 до 80 символов.")]
    public string Name { get; set; } = "";
    [Required(ErrorMessage = "Укажите телефон."), StringLength(40)]
    public string Phone { get; set; } = "";
    [Required(ErrorMessage = "Укажите почту."), EmailAddress(ErrorMessage = "Укажите корректную почту."), StringLength(150)]
    public string Email { get; set; } = "";
    [StringLength(3000, ErrorMessage = "Сообщение слишком длинное.")]
    public string Message { get; set; } = "";
    public bool Consent { get; set; }
    public string? Website { get; set; }
    public string ReturnTo { get; set; } = "/contact";
    public bool HasCalculation { get; set; }
    public int? KitchenId { get; set; }
    public Configuration Config { get; set; } = new Configuration();
    public Kitchen? Model { get; set; }
    public Quote? Quote { get; set; }
}
public class CatalogPage
{
    [StringLength(100)]
    public string? Q { get; set; }
    [StringLength(80)]
    public string? Style { get; set; }
    [Range(0, 10000000)]
    public decimal? Min { get; set; }
    [Range(0, 10000000)]
    public decimal? Max { get; set; }
    [Range(typeof(decimal), "0.6", "8")]
    public decimal? Length { get; set; }
    public string Sort { get; set; } = "popular";
    public List<Kitchen> Kitchens { get; set; } = new List<Kitchen>();
    public List<string> Styles { get; set; } = new List<string>();
}
public class HomePage
{
    public List<Kitchen> Kitchens { get; set; } = new List<Kitchen>();
    public List<Project> Projects { get; set; } = new List<Project>();
    public List<Review> Reviews { get; set; } = new List<Review>();
    public RequestForm Calculator { get; set; } = new RequestForm();
}
public class RequestList
{
    public string? Q { get; set; }
    public string? Status { get; set; }
    public string Sort { get; set; } = "newest";
    public int P { get; set; } = 1;
    public int Count { get; set; }
    public int Pages { get; set; } = 1;
    public List<Inquiry> Rows { get; set; } = new List<Inquiry>();
}
public class ContentGroup
{
    public string Label { get; set; } = "";
    public Dictionary<string, string> Fields { get; set; } = new Dictionary<string, string>();
}
public class AdminPage
{
    public bool LoginPage { get; set; }
    public string Login { get; set; } = "";
    public string Section { get; set; } = "dashboard";
    public string Group { get; set; } = "company";
    public bool Editing { get; set; }
    public int Pending { get; set; }
    public int Moderation { get; set; }
    public Dictionary<string, int> Counts { get; set; } = new Dictionary<string, int>();
    public RequestList Requests { get; set; } = new RequestList();
    public Inquiry? Inquiry { get; set; }
    public Quote? Snapshot { get; set; }
    public List<Kitchen> Kitchens { get; set; } = new List<Kitchen>();
    public List<Project> Projects { get; set; } = new List<Project>();
    public List<Review> Reviews { get; set; } = new List<Review>();
    public Kitchen Kitchen { get; set; } = new Kitchen();
    public Project Project { get; set; } = new Project();
    public Review Review { get; set; } = new Review();
    public Configuration Config { get; set; } = new Configuration();
    public bool OnlyPending { get; set; }
}
