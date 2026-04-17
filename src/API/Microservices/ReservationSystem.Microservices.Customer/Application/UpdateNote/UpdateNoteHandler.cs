using ReservationSystem.Microservices.Customer.Domain.Repositories;

namespace ReservationSystem.Microservices.Customer.Application.UpdateNote;

public sealed class UpdateNoteHandler
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerNoteRepository _noteRepository;

    public UpdateNoteHandler(ICustomerRepository customerRepository, ICustomerNoteRepository noteRepository)
    {
        _customerRepository = customerRepository;
        _noteRepository = noteRepository;
    }

    /// <returns>True if updated; false if customer or note not found or note belongs to a different customer.</returns>
    public async Task<bool> HandleAsync(UpdateNoteCommand command, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByLoyaltyNumberAsync(command.LoyaltyNumber, cancellationToken);
        if (customer is null)
            return false;

        var note = await _noteRepository.GetByIdAsync(command.NoteId, cancellationToken);
        if (note is null || note.CustomerId != customer.CustomerId)
            return false;

        note.UpdateText(command.NoteText);
        await _noteRepository.UpdateAsync(note, cancellationToken);
        return true;
    }
}
