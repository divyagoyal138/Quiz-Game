using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using quiz_game_mvc.Models;

namespace quiz_game_mvc.Controllers
{
    public class QuizController : Controller
    {
        private readonly IConfiguration _config;

        public QuizController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet]
        public IActionResult Index()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = new QuizViewModel
            {
                Username = HttpContext.Session.GetString("Username") ?? string.Empty,
                Difficulty = "Easy",
                TimerSeconds = 60,
                IsStarted = false
            };

            LoadLeaderboard(model);

            return View(model);
        }

        [HttpPost]
        public IActionResult Start(QuizViewModel model)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Login", "Account");
            }

            model.Username = HttpContext.Session.GetString("Username") ?? string.Empty;
            model.IsStarted = true;
            model.TimerSeconds = string.Equals(model.Difficulty, "Hard", StringComparison.OrdinalIgnoreCase)
                ? 30
                : 60;

            LoadQuestions(model);
            LoadLeaderboard(model);

            return View("Index", model);
        }

        [HttpPost]
        public IActionResult Index(QuizViewModel model)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Login", "Account");
            }

            model.Username = HttpContext.Session.GetString("Username") ?? string.Empty;
            model.IsStarted = true;
            LoadQuestions(model);

            int score = 0;
            model.LastCorrectness = new List<bool>(model.Questions.Count);

            for (int i = 0; i < model.Questions.Count; i++)
            {
                int? selected = (i < model.Answers.Count) ? model.Answers[i] : null;
                bool isCorrect = selected.HasValue && selected.Value == model.Questions[i].CorrectIndex;
                model.LastCorrectness.Add(isCorrect);
                if (isCorrect) score++;
            }

            model.LastScore = score;
            SaveScoreAndOptionalFeedback(score, model.Feedback);
            LoadLeaderboard(model);

            return View(model);
        }

        [HttpPost]
        public IActionResult Feedback(QuizViewModel model)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Login", "Account");
            }

            model.Username = HttpContext.Session.GetString("Username") ?? string.Empty;
            LoadQuestions(model);
            SaveFeedbackOnly(model.Feedback);
            model.FeedbackSaved = true;
            LoadLeaderboard(model);

            return View("Index", model);
        }

        private void LoadQuestions(QuizViewModel model)
        {
            var allQuestions = new List<QuizViewModel.QuizQuestion>
            {
                new("What is 5 + 3?", new[] { "6", "8", "10", "12" }, 1),
                new("What is the capital of France?", new[] { "Berlin", "Madrid", "Paris", "Rome" }, 2),
                new("Which one is a programming language?", new[] { "HTML", "C#", "HTTP", "SQL Server" }, 1),
                new("What is 9 × 2?", new[] { "18", "11", "12", "20" }, 0),
                new("Which planet is known as the Red Planet?", new[] { "Venus", "Mars", "Jupiter", "Saturn" }, 1),
            };

            // Add more questions
            allQuestions.AddRange(new[]
            {
                new QuizViewModel.QuizQuestion("What is 10 ÷ 2?", new[] { "3", "4", "5", "6" }, 2),
                new QuizViewModel.QuizQuestion("Which is NOT a planet?", new[] { "Earth", "Pluto", "Mars", "Sirius" }, 3),
                new QuizViewModel.QuizQuestion("Which company created C#?", new[] { "Apple", "Google", "Microsoft", "IBM" }, 2),
                new QuizViewModel.QuizQuestion("What does CPU stand for?", new[] { "Central Processing Unit", "Computer Personal Unit", "Central Print Unit", "Control Processing Utility" }, 0),
                new QuizViewModel.QuizQuestion("Which one is a web browser?", new[] { "Windows", "Chrome", "Linux", "Android" }, 1)
            });

            // Shuffle deterministically based on Seed so that POST requests
            // reconstruct the same order used when the game started.
            if (model.Seed == 0)
            {
                model.Seed = Random.Shared.Next();
            }

            var rand = new Random(model.Seed);
            model.Questions = allQuestions
                .OrderBy(_ => rand.Next())
                .ToList();

            if (model.Answers.Count < model.Questions.Count)
            {
                for (int i = model.Answers.Count; i < model.Questions.Count; i++)
                {
                    model.Answers.Add(null);
                }
            }
        }

        private void LoadLeaderboard(QuizViewModel model)
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            string connStr = _config.GetConnectionString("MySql")!;

            using var conn = new MySqlConnection(connStr);
            conn.Open();

            // Top 10 (by max score per user)
            string topQuery = @"
                SELECT u.username, MAX(s.score) AS highscore
                FROM users u
                JOIN scores s ON u.id = s.user_id
                GROUP BY u.username
                ORDER BY highscore DESC
                LIMIT 10";

            using (var cmd = new MySqlCommand(topQuery, conn))
            using (var reader = cmd.ExecuteReader())
            {
                model.TopScores.Clear();
                while (reader.Read())
                {
                    model.TopScores.Add((
                        reader.GetString("username"),
                        reader.GetInt32("highscore")
                    ));
                }
            }

            // Global high score (single best score)
            string globalQuery = @"
                SELECT u.username, s.score
                FROM scores s
                JOIN users u ON u.id = s.user_id
                ORDER BY s.score DESC
                LIMIT 1";

            using (var cmd = new MySqlCommand(globalQuery, conn))
            using (var reader = cmd.ExecuteReader())
            {
                model.GlobalHighScore = null;
                if (reader.Read())
                {
                    model.GlobalHighScore = (reader.GetString("username"), reader.GetInt32("score"));
                }
            }

            // Your personal high score
            string yourQuery = "SELECT MAX(score) FROM scores WHERE user_id = @u";
            using (var cmd = new MySqlCommand(yourQuery, conn))
            {
                cmd.Parameters.AddWithValue("@u", userId);
                object? result = cmd.ExecuteScalar();
                model.YourHighScore = result == DBNull.Value || result == null ? 0 : Convert.ToInt32(result);
            }
        }

        private void SaveScoreAndOptionalFeedback(int score, string? feedback)
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (userId == 0) return;

            string connStr = _config.GetConnectionString("MySql")!;

            using var conn = new MySqlConnection(connStr);
            conn.Open();

            // Save score
            using (var scoreCmd = new MySqlCommand(
                "INSERT INTO scores (user_id, score) VALUES (@u, @s)", conn))
            {
                scoreCmd.Parameters.AddWithValue("@u", userId);
                scoreCmd.Parameters.AddWithValue("@s", score);
                scoreCmd.ExecuteNonQuery();
            }

            // Save feedback (optional)
            if (!string.IsNullOrWhiteSpace(feedback))
            {
                using var fbCmd = new MySqlCommand(
                    "INSERT INTO feedback (user_id, message) VALUES (@u, @m)", conn);
                fbCmd.Parameters.AddWithValue("@u", userId);
                fbCmd.Parameters.AddWithValue("@m", feedback.Trim());
                fbCmd.ExecuteNonQuery();
            }
        }

        private void SaveFeedbackOnly(string? feedback)
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (userId == 0) return;
            if (string.IsNullOrWhiteSpace(feedback)) return;

            string connStr = _config.GetConnectionString("MySql")!;

            using var conn = new MySqlConnection(connStr);
            conn.Open();

            using var fbCmd = new MySqlCommand(
                "INSERT INTO feedback (user_id, message) VALUES (@u, @m)", conn);
            fbCmd.Parameters.AddWithValue("@u", userId);
            fbCmd.Parameters.AddWithValue("@m", feedback.Trim());
            fbCmd.ExecuteNonQuery();
        }
    }
}

