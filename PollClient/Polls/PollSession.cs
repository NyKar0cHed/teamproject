using System.Collections.Generic;
using System.Linq;

namespace PollClient.Polls
{
    public class PollQuestion
    {
        public int Index { get; }
        public string Text { get; set; } = "";
        public List<string> Options { get; } = new();
        public PollQuestion(int index) => Index = index;
    }

    public class PollSession
    {
        private Dictionary<int, int> _answers = new();
        public int Id { get; }
        public List<PollQuestion> Questions { get; } = new();
        public bool IsComplete { get; set; }

        public PollSession(int id) => Id = id;

        public PollQuestion GetOrCreateQuestion(int index)
        {
            while (Questions.Count <= index)
                Questions.Add(new PollQuestion(Questions.Count));
            return Questions[index];
        }

        public void SetAnswer(int questionIndex, int optionIndex) =>
            _answers[questionIndex] = optionIndex;

        public bool TryGetAnswer(int index, out int option) =>
            _answers.TryGetValue(index, out option);

        public bool HasAllAnswers =>
            Questions.Count > 0 && Enumerable.Range(0, Questions.Count).All(_answers.ContainsKey);

        public string BuildSubmission() =>
            string.Join(",", Enumerable.Range(0, Questions.Count)
                .Where(_answers.ContainsKey).Select(i => $"{i}:{_answers[i]}"));
    }
}
