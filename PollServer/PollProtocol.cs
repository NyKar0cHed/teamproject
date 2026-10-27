namespace PollServer
{
    public static class PollProtocol
    {
        public const string Welcome = "WELCOME";
        public const string Exit = "EXIT";
        public const string Bye = "BYE";

        public const string NewPoll = "NEW_POLL";
        public const string PollQuestion = "POLL_QUESTION";
        public const string PollOption = "POLL_OPTION";
        public const string EndPoll = "END_POLL";

        public const string StartPoll = "START_POLL";
        public const string SubmitPoll = "SUBMIT_POLL";

        public const string CreateStarted = "CREATE_STARTED";
        public const string QuestionReceived = "QUESTION_RECEIVED";
        public const string OptionReceived = "OPTION_RECEIVED";
        public const string PollCreated = "POLL_CREATED";
        public const string PollStarted = "POLL_STARTED";

        public const string Poll = "POLL";
        public const string Question = "QUESTION";
        public const string Option = "OPTION";
        public const string EndPollData = "END_POLL";

        public const string Result = "RESULT";
        public const string Error = "ERROR";
    }
}
