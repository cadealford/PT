using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetroTransit.API.Models
{
    public class Reservation
    {
        [Key]
        public int Id { get; set; }

        // Start and End times are stored as UTC in the database
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        // public bool IsAllDay { get; set; } <-- REMOVED PROPERTY

        public string Location { get; set; } = string.Empty;
        public string FlightDetails { get; set; } = string.Empty;
        public int PassengerCount { get; set; }
        public string Notes { get; set; } = string.Empty;

        // Foreign Keys
        public int AirplaneId { get; set; }
        public int PersonnelId { get; set; }
        
        // Relationship Properties
        [ForeignKey("AirplaneId")]
        public Airplane? Airplane { get; set; }

        [ForeignKey("PersonnelId")]
        public Personnel? Personnel { get; set; }
        
        // Owner (Identity User)
        public string CreatedByUserId { get; set; } = string.Empty;
        [ForeignKey("CreatedByUserId")]
        public AppUser? CreatedByUser { get; set; }
    }
}