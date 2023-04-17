using ImageSharePassword.Web.Models;
using Microsoft.AspNetCore.Mvc;
using ImageSharePassword.Data;
using System.Text.Json;

namespace ImageSharePassword.Web.Controllers
{
    public class HomeController : Controller
    {
        private string _connectionString =
            @"Data Source=.\sqlexpress;Initial Catalog=ImageSharePassword;Integrated Security=true";

        private IWebHostEnvironment _environment;

        public HomeController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Upload(Image image, IFormFile imageFile)
        {
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(imageFile.FileName)}";
            string fullPath = Path.Combine(_environment.WebRootPath, "uploads", fileName);
            using var stream = new FileStream(fullPath, FileMode.CreateNew);
            imageFile.CopyTo(stream);
            image.FileName = fileName;
            var db = new ImageDb(_connectionString);
            db.Add(image);
            return View(image);
        }

        public ActionResult ViewImage(int id)
        {
            var viewModel = new ViewImageViewModel();
            if (TempData["message"] != null)
            {
                viewModel.Message = (string)TempData["message"];
            }

            if (!HasPermissionToView(id))
            {
                viewModel.HasPermissionToView = false;
                viewModel.Image = new Image { Id = id };
            }
            else
            {
                viewModel.HasPermissionToView = true;
                var db = new ImageDb(_connectionString);
                db.IncrementViewCount(id);
                var image = db.GetById(id);
                if (image == null)
                {
                    return RedirectToAction("Index");
                }

                viewModel.Image = image;
            }

            return View(viewModel);
        }

        private bool HasPermissionToView(int id)
        {
            var allowedIds = HttpContext.Session.Get<List<int>>("allowedids");
            if (allowedIds == null)
            {
                return false;
            }

            return allowedIds.Contains(id);
        }

        [HttpPost]
        public ActionResult ViewImage(int id, string password)
        {
            var db = new ImageDb(_connectionString);
            var image = db.GetById(id);
            if (image == null)
            {
                return RedirectToAction("Index");
            }

            if (password != image.Password)
            {
                TempData["message"] = "Invalid password";
            }
            else
            {
                var allowedIds = HttpContext.Session.Get<List<int>>("allowedids");
                if (allowedIds == null)
                {
                    allowedIds = new List<int>();
                }
                allowedIds.Add(id);
                HttpContext.Session.Set("allowedids", allowedIds);
            }

            return Redirect($"/home/viewimage?id={id}");
        }
    }

    public static class SessionExtensions
    {
        public static void Set<T>(this ISession session, string key, T value)
        {
            session.SetString(key, JsonSerializer.Serialize(value));
        }

        public static T Get<T>(this ISession session, string key)
        {
            string value = session.GetString(key);

            return value == null ? default(T) :
                JsonSerializer.Deserialize<T>(value);
        }
    }
}

