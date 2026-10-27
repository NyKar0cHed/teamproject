using System;
using System.Collections.Generic;
using System.Text;

namespace PollServer
{
    // TEMP CLASS //

    public class PollResponse
    {
        public int PollId { get; set; }
        public int QuestionId { get; set; }

        public string ClientId { get; set; } = "";

        public int OptionNumber { get; set; }

        public string Answer { get; set; } = "";

        public DateTime ReceivedAt { get; set; } = DateTime.Now;
    }
}
