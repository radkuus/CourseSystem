using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CourseSystem.Data.Models
{
    public class Submission
    {
        public Guid Id { get; set; }

        [Required]
        public Guid AssignmentId { get; set; }

        [Required]
        public Guid StudentId { get; set; }

        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        public DateTime SubmittedAt { get; set; }

        public float? Grade { get; set; }

        // Navigation properties
        [ForeignKey("AssignmentId")]
        public virtual Assignment Assignment { get; set; } = null!;

        [ForeignKey("StudentId")]
        public virtual User Student { get; set; } = null!;
    }
}
