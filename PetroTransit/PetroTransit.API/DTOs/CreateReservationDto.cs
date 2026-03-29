using System.ComponentModel.DataAnnotations;

namespace PetroTransit.API.DTOs;

public class CreateReservationDto
{
    [Required]
    public DateTime StartTime { get; set; }

    [Required]
    public DateTime EndTime { get; set; }

    public bool IsAllDay { get; set; }

    [Required]
    public int AirplaneId { get; set; }

    [Required] // This is the ID of the person the flight is for (from the dropdown)
    public int PersonnelId { get; set; }

    // Text fields
    public string Location { get; set; } = string.Empty;
    public string FlightDetails { get; set; } = string.Empty;
    public int PassengerCount { get; set; }
    public string Notes { get; set; } = string.Empty;
}