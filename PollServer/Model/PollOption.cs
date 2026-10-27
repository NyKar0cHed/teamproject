using System;
using System.Collections.Generic;
using System.Text;

namespace PollServer.Model
{
    public class PollOption
    {
        public int Id { get; set; }
        public string Text { get; set; } = "";

        public int PollQuestionId { get; set; }
        public PollQuestion? PollQuestion { get; set; }
    }
}
