using Kuhni.Models;
using Microsoft.AspNetCore.Mvc;
namespace Kuhni.Controllers;

public class HomeController : Controller
{
    private readonly Data db;
    public HomeController(Data data) { db = data; }
    [HttpGet("/"), HttpGet("/home")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Главная";
        HomePage page = new HomePage
        {
            Kitchens = db.Kitchens().Take(3).ToList(),
            Projects = db.Projects().Take(2).ToList(),
            Reviews = db.Reviews().Take(2).ToList(),
            Calculator = Calculator.Form(null, db.Pricing, "/home")
        };
        return View(page);
    }
}
