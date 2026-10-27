using PollServer.Model;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace PollServer.Data
{
    public class PollContext : DbContext
    {
        public PollContext()
        {

        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(
                @"Server=(localdb)\MSSQLLocalDB;Database=s104_ZeVybory;Trusted_Connection=True;MultipleActiveResultSets=true");
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<Poll> Polls { get; set; }
        public DbSet<PollQuestion> PollQuestions { get; set; }
        public DbSet<PollOption> PollOptions { get; set; }
        public DbSet<Admin> Admins { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasOne(e => e.Admin)
                .WithOne(p => p.User)
                .HasForeignKey<Admin>(p => p.UserId);
        }
    }
}
