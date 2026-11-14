using Kuhni.Models;
using Microsoft.AspNetCore.Identity;
using System.Text.RegularExpressions;
namespace Kuhni;

public static class Setup
{
    public static int Run(Data db)
    {
        try
        {
            if (db.HasAdmin)
            {
                Console.WriteLine("Администратор уже создан. Логин и пароль меняются в панели.");
                return 0;
            }
            Console.Write("Логин администратора: ");
            string login = (Console.ReadLine() ?? "").Trim();
            Console.Write("Пароль (от 10 символов): ");
            string password = ReadPassword();
            Console.Write("Повторите пароль: ");
            string repeat = ReadPassword();
            if (!Regex.IsMatch(login, @"^[A-Za-z0-9_.-]{3,80}$") || password.Length < 10 || password.Length > 200 || password != repeat)
            {
                Console.WriteLine("Проверьте логин и пароли. Логин: от 3 латинских символов, пароль: от 10 символов.");
                return 1;
            }
            Admin admin = new Admin { Login = login };
            admin.Password = new PasswordHasher<Admin>().HashPassword(admin, password);
            db.SaveAdmin(admin);
            Console.WriteLine("Администратор создан. Вход на сайте: /admin.");
            return 0;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            Console.WriteLine("Не удалось сохранить данные. Проверьте права записи в папку AppData.");
            return 1;
        }
    }
    private static string ReadPassword()
    {
        if (Console.IsInputRedirected) return Console.ReadLine() ?? "";
        string password = "";
        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return password; }
            if (key.Key == ConsoleKey.Backspace) { if (password.Length > 0) password = password[..^1]; }
            else if (!char.IsControl(key.KeyChar)) password += key.KeyChar;
        }
    }
}
