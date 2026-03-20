namespace ReservationSystem.Template.TemplateApi.Domain.Entities;

/// <summary>
/// Core domain entity representing a person record in [dbo].[Persons].
/// Contains business state and enforces invariants.
/// Has no dependency on infrastructure, persistence, or serialisation concerns.
/// </summary>
public sealed class Person
{
    public int PersonID { get; private set; }
    public string LastName { get; private set; } = string.Empty;
    public string? FirstName { get; private set; }
    public string? Address { get; private set; }
    public string? City { get; private set; }

    private Person() { }

    /// <summary>
    /// Factory method for creating a brand-new person record.
    /// PersonID must be supplied by the caller — there is no IDENTITY constraint on the table.
    /// </summary>
    public static Person Create(int personId, string lastName, string? firstName, string? address, string? city)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        return new Person
        {
            PersonID = personId,
            LastName = lastName,
            FirstName = firstName,
            Address = address,
            City = city
        };
    }

    /// <summary>
    /// Factory method for reconstituting an entity from a persistence store.
    /// </summary>
    public static Person Reconstitute(int personId, string lastName, string? firstName, string? address, string? city)
    {
        return new Person
        {
            PersonID = personId,
            LastName = lastName,
            FirstName = firstName,
            Address = address,
            City = city
        };
    }

    public void Update(string lastName, string? firstName, string? address, string? city)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
        LastName = lastName;
        FirstName = firstName;
        Address = address;
        City = city;
    }
}
