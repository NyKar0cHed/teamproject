using System;
using System.Collections.Generic;
using System.Text;

namespace PollServer.Model
{
    public class Group
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";

        public List<User> Users { get; set; } = new();
    }
}
