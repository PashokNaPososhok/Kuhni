using System.ComponentModel.DataAnnotations;
namespace Kuhni.Models;

public class Kitchen
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Укажите название."), StringLength(150, MinimumLength = 2)]
    public string Name { get; set; } = "";
    [Required(ErrorMessage = "Укажите стиль."), StringLength(80)]
    public string Style { get; set; } = "Современный";
    [Required(ErrorMessage = "Добавьте описание."), StringLength(12000)]
    public string Description { get; set; } = "";
    [StringLength(500)]
    public string Image { get; set; } = "assets/img/kitchen-1.jpg";
    [StringLength(10000)]
    public string Gallery { get; set; } = "";
    public Configuration Config { get; set; } = new();
    [Range(0, 1000000)]
    public decimal Surcharge { get; set; }
    [Range(-100000, 100000)]
    public int SortOrder { get; set; }
}
