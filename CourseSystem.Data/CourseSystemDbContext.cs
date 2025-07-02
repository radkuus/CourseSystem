using CourseSystem.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CourseSystem.Data
{
    public class CourseSystemDbContext : DbContext
    {
        public CourseSystemDbContext(DbContextOptions<CourseSystemDbContext> options) : base(options)
        {
        }


        public DbSet<User> Users { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<Assignment> Assignments { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }
        public DbSet<Submission> Submissions { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.Role).HasConversion<string>();
            });

            // Course configuration
            modelBuilder.Entity<Course>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.Status).HasConversion<string>();

                entity.HasOne(e => e.Owner)
                      .WithMany(u => u.OwnedCourses)
                      .HasForeignKey(e => e.OwnerId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Assignment configuration
            modelBuilder.Entity<Assignment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasOne(e => e.Course)
                      .WithMany(c => c.Assignments)
                      .HasForeignKey(e => e.CourseId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Enrollment configuration
            modelBuilder.Entity<Enrollment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.EnrolledAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.Status).HasConversion<string>();

                entity.HasOne(e => e.Course)
                      .WithMany(c => c.Enrollments)
                      .HasForeignKey(e => e.CourseId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Student)
                      .WithMany(u => u.Enrollments)
                      .HasForeignKey(e => e.StudentId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Unique constraint: student can enroll only once per course
                entity.HasIndex(e => new { e.CourseId, e.StudentId }).IsUnique();
            });

            // Submission configuration
            modelBuilder.Entity<Submission>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.SubmittedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasOne(e => e.Assignment)
                      .WithMany(a => a.Submissions)
                      .HasForeignKey(e => e.AssignmentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Student)
                      .WithMany(u => u.Submissions)
                      .HasForeignKey(e => e.StudentId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Unique constraint: student can submit only once per assignment
                entity.HasIndex(e => new { e.AssignmentId, e.StudentId }).IsUnique();
            });

            // Notification configuration
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasOne(e => e.Recipient)
                      .WithMany(u => u.Notifications)
                      .HasForeignKey(e => e.RecipientId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Assignment)
                      .WithMany(a => a.Notifications)
                      .HasForeignKey(e => e.AssignmentId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.Course)
                      .WithMany(c => c.Notifications)
                      .HasForeignKey(e => e.CourseId)
                      .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
