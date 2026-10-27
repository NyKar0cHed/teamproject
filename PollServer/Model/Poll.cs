using System;
using System.Collections.Generic;
using System.Text;

namespace PollServer.Model
{
    public class Poll
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public List<PollQuestion> Questions { get; set; } = new();
    }
}
