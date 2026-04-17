using ReservationSystem.Microservices.Customer.Domain.Entities;
using ReservationSystem.Microservices.Customer.Domain.Repositories;

namespace ReservationSystem.Microservices.Customer.Application.AddNote;

public sealed class AddNoteHandler
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerNoteRepository _noteRepository;

    public AddNoteHandler(ICustomerRepository customerRepository, ICustomerNoteRepository noteRepository)
    {
        _customerRepository = customerRepository;
        _noteRepository = noteRepository;
    }

    /// <returns>The new note, or null if customer not found.</returns>
    public async Task<CustomerNote?> HandleAsync(AddNoteCommand command, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByLoyaltyNumberAsync(command.LoyaltyNumber, cancellationToken);
        if (customer is null)
            return null;

        var note = CustomerNote.Create(customer.CustomerId, command.NoteText, command.CreatedBy);
        await _noteRepository.AddAsync(note, cancellationToken);
        return note;
    }
}
