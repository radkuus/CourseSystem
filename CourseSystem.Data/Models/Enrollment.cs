using CourseSystem.Data.Models.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CourseSystem.Data.Models
{
    public class Enrollment
    {
        public Guid Id { get; set; }

        [Required]
        public Guid CourseId { get; set; }

        [Required]
        public Guid StudentId { get; set; }

        [Required]
        public EnrollmentStatus Status { get; set; }

        public DateTime EnrolledAt { get; set; }

        // Navigation properties
        [ForeignKey("CourseId")]
        public virtual Course Course { get; set; } = null!;

        [ForeignKey("StudentId")]
        public virtual User Student { get; set; } = null!;
    }
}
