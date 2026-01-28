using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using quiz_game_mvc.Models;
using System.Security.Cryptography;
using System.Text;

namespace quiz_game_mvc.Controllers
{
    public class AccountController : Controller
    {
        private readonly IConfiguration _config;

        public AccountController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        [HttpPost]
        public IActionResult Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string hashedPassword = HashPassword(model.Password);
            string connStr = _config.GetConnectionString("MySql")!;

            using var conn = new MySqlConnection(connStr);
            conn.Open();

            string query = "INSERT INTO users (username, email, password) VALUES (@u, @e, @p)";
            using var cmd = new MySqlCommand(query, conn);

            cmd.Parameters.AddWithValue("@u", model.Username);
            cmd.Parameters.AddWithValue("@e", model.Email);
            cmd.Parameters.AddWithValue("@p", hashedPassword);

            try
            {
                cmd.ExecuteNonQuery();
                model.Message = "Registration successful!";
            }
            catch
            {
                model.Message = "Email already exists.";
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View(new LoginViewModel());
        }

        [HttpPost]
        public IActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string connStr = _config.GetConnectionString("MySql")!;
            string hashedPassword = HashPassword(model.Password);

            using var conn = new MySqlConnection(connStr);
            conn.Open();

            string query = "SELECT id, username FROM users WHERE email=@e AND password=@p";
            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@e", model.Email);
            cmd.Parameters.AddWithValue("@p", hashedPassword);

            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                HttpContext.Session.SetInt32("UserId", reader.GetInt32("id"));
                HttpContext.Session.SetString("Username", reader.GetString("username"));
                return RedirectToAction("Index", "Quiz");
            }

            model.Message = "Invalid email or password";
            return View(model);
        }

        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return View();
        }

        private static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }
    }
}

