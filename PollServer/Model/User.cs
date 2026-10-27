using System;
using System.Collections.Generic;
using System.Text;

namespace PollServer.Model
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Password { get; set; } = "";
        public bool IsBanned { get; set; } = false;
        public Admin? Admin { get; set; }
        
        public List<Group> Groups { get; set; } = new();
    }
}
