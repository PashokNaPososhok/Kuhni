using System.ComponentModel.DataAnnotations;
namespace Kuhni.Models;

public class Review
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Укажите имя."), StringLength(80, MinimumLength = 2)]
    public string Name { get; set; } = "";
    [Range(1, 5, ErrorMessage = "Выберите оценку от 1 до 5.")]
    public int Rating { get; set; } = 5;
    [Required(ErrorMessage = "Напишите отзыв."), StringLength(2000, MinimumLength = 10, ErrorMessage = "Отзыв должен содержать от 10 до 2000 символов.")]
    public string Text { get; set; } = "";
    public bool Approved { get; set; }
    [Range(-100000, 100000)]
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}
