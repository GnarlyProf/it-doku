using Microsoft.AspNetCore.Mvc;

namespace ITDoku.Controllers;

public class HomeController : Controller
{
    public IActionResult Error() => View();
}
