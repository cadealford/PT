namespace PetroTransit.API.Models;

public class Airplane
{
    public int Id { get; set; }
    public required string Name { get; set; } // e.g., "HWK"
    public string RegistrationNumber { get; set; } = string.Empty;
    public int Capacity { get; set; }
}