namespace quiz_game_mvc.Models
{
    public class QuizViewModel
    {
        public string? Username { get; set; }
        public List<int?> Answers { get; set; } = new();
        public string? Feedback { get; set; }
        public bool FeedbackSaved { get; set; }

        public record QuizQuestion(string Text, string[] Options, int CorrectIndex);

        public List<QuizQuestion> Questions { get; set; } = new();
        public int? LastScore { get; set; }
        public List<bool> LastCorrectness { get; set; } = new();
        public List<(string Username, int Score)> TopScores { get; set; } = new();
        public (string Username, int Score)? GlobalHighScore { get; set; }
        public int? YourHighScore { get; set; }

        public int TimerSeconds { get; set; } = 60;
        public string Difficulty { get; set; } = "Easy";
        public bool IsStarted { get; set; }
        public int Seed { get; set; }
    }
}

