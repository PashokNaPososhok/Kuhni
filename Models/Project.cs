using System.ComponentModel.DataAnnotations;
namespace Kuhni.Models;

public class Project
{
    public int Id { get; set; }
    public int? KitchenId { get; set; }
    public string? KitchenName { get; set; }
    [Required(ErrorMessage = "Укажите название."), StringLength(150, MinimumLength = 2)]
    public string Name { get; set; } = "";
    [StringLength(300)]
    public string Summary { get; set; } = "";
    [Required(ErrorMessage = "Добавьте описание."), StringLength(12000)]
    public string Description { get; set; } = "";
    [StringLength(500)]
    public string Image { get; set; } = "assets/img/kitchen-1.jpg";
    [StringLength(10000)]
    public string Gallery { get; set; } = "";
    [StringLength(80)]
    public string Area { get; set; } = "";
    [StringLength(100)]
    public string City { get; set; } = "Ижевск";
    [StringLength(80)]
    public string Duration { get; set; } = "";
    [Range(-100000, 100000)]
    public int SortOrder { get; set; }
}
