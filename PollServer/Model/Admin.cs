using System;
using System.Collections.Generic;
using System.Text;

namespace PollServer.Model
{
    public class Admin
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
    }
}
