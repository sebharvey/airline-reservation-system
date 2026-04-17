using ReservationSystem.Microservices.Customer.Domain.Entities;
using ReservationSystem.Microservices.Customer.Domain.Repositories;

namespace ReservationSystem.Microservices.Customer.Application.GetNotes;

public sealed class GetNotesHandler
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerNoteRepository _noteRepository;

    public GetNotesHandler(ICustomerRepository customerRepository, ICustomerNoteRepository noteRepository)
    {
        _customerRepository = customerRepository;
        _noteRepository = noteRepository;
    }

    /// <returns>Notes ordered most-recent-first, or null if customer not found.</returns>
    public async Task<IReadOnlyList<CustomerNote>?> HandleAsync(GetNotesQuery query, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByLoyaltyNumberAsync(query.LoyaltyNumber, cancellationToken);
        if (customer is null)
            return null;

        return await _noteRepository.GetByCustomerIdAsync(customer.CustomerId, cancellationToken);
    }
}
