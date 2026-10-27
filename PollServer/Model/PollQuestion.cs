using System;
using System.Collections.Generic;
using System.Text;

namespace PollServer.Model
{
    public class PollQuestion
    {
        public int Id { get; set; }
        public string Text { get; set; } = "";

        public int PollId { get; set; }
        public Poll? Poll { get; set; }

        public List<PollOption> Options { get; set; } = new();
    }
}
