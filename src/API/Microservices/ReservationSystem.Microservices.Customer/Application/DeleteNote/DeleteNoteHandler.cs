using ReservationSystem.Microservices.Customer.Domain.Repositories;

namespace ReservationSystem.Microservices.Customer.Application.DeleteNote;

public sealed class DeleteNoteHandler
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerNoteRepository _noteRepository;

    public DeleteNoteHandler(ICustomerRepository customerRepository, ICustomerNoteRepository noteRepository)
    {
        _customerRepository = customerRepository;
        _noteRepository = noteRepository;
    }

    /// <returns>True if deleted; false if customer or note not found or note belongs to a different customer.</returns>
    public async Task<bool> HandleAsync(DeleteNoteCommand command, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByLoyaltyNumberAsync(command.LoyaltyNumber, cancellationToken);
        if (customer is null)
            return false;

        var note = await _noteRepository.GetByIdAsync(command.NoteId, cancellationToken);
        if (note is null || note.CustomerId != customer.CustomerId)
            return false;

        await _noteRepository.DeleteAsync(note, cancellationToken);
        return true;
    }
}
