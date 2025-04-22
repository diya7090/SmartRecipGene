using System;

namespace SmartRecipGene.Models
{
    public class UserActivity
    {
        public int Id { get; set; } // Primary Key
        public string UserId { get; set; } // Foreign Key
        public ApplicationUser User { get; set; } // Navigation Property
        public string ActivityType { get; set; } // Example: "Saved Recipe", "Login", etc.
        public DateTime ActivityDate { get; set; } = DateTime.UtcNow; // Timestamp
    }
}
