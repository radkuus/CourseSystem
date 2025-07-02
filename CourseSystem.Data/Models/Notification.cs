using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CourseSystem.Data.Models
{
    public class Notification
    {
        public Guid Id { get; set; }

        [Required]
        public Guid RecipientId { get; set; }

        public Guid? AssignmentId { get; set; }

        public Guid? CourseId { get; set; }

        [Required]
        [MaxLength(1000)]
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        // Navigation properties
        [ForeignKey("RecipientId")]
        public virtual User Recipient { get; set; } = null!;

        [ForeignKey("AssignmentId")]
        public virtual Assignment? Assignment { get; set; }

        [ForeignKey("CourseId")]
        public virtual Course? Course { get; set; }
    }
}
