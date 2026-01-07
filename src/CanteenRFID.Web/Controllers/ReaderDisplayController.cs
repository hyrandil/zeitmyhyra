using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanteenRFID.Web.Controllers;

[AllowAnonymous]
public class ReaderDisplayController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}
