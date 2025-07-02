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
    public class Course
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public Guid OwnerId { get; set; }

        [Required]
        public int AcademicYear { get; set; }

        [Required]
        public CourseStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }

        // Navigation properties
        [ForeignKey("OwnerId")]
        public virtual User Owner { get; set; } = null!;

        public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
        public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    }
}
