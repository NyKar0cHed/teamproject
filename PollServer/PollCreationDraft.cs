using System.Collections.Generic;

namespace PollServer
{
    internal sealed class PollCreationDraft
    {
        public int ExpectedQuestionCount { get; }
        public Dictionary<int, string> Questions { get; } = new();
        public Dictionary<int, List<string>> Options { get; } = new();

        public PollCreationDraft(int expectedQuestionCount)
        {
            ExpectedQuestionCount = expectedQuestionCount;
        }
    }
}
