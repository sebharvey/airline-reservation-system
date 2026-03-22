using ReservationSystem.Microservices.Customer.Application.CreateCustomer;
using ReservationSystem.Microservices.Customer.Application.UpdateCustomer;
using ReservationSystem.Microservices.Customer.Application.AuthorisePoints;
using ReservationSystem.Microservices.Customer.Application.SettlePoints;
using ReservationSystem.Microservices.Customer.Application.ReversePoints;
using ReservationSystem.Microservices.Customer.Application.ReinstatePoints;
using ReservationSystem.Microservices.Customer.Models.Requests;
using ReservationSystem.Microservices.Customer.Models.Responses;

namespace ReservationSystem.Microservices.Customer.Models.Mappers;

/// <summary>
/// Static mapping methods between all model representations of a Customer.
///
/// Mapping directions:
///   HTTP request  →  Application command/query
///   Domain entity  →  HTTP response
/// </summary>
public static class CustomerMapper
{
    // -------------------------------------------------------------------------
    // HTTP request → Application command
    // -------------------------------------------------------------------------

    public static CreateCustomerCommand ToCommand(string loyaltyNumber, CreateCustomerRequest request) =>
        new(
            LoyaltyNumber: loyaltyNumber,
            GivenName: request.GivenName,
            Surname: request.Surname,
            DateOfBirth: request.DateOfBirth,
            PreferredLanguage: request.PreferredLanguage,
            IdentityReference: request.IdentityReference);

    public static UpdateCustomerCommand ToCommand(string loyaltyNumber, UpdateCustomerRequest request) =>
        new(
            LoyaltyNumber: loyaltyNumber,
            GivenName: request.GivenName,
            Surname: request.Surname,
            DateOfBirth: request.DateOfBirth,
            Nationality: request.Nationality,
            PhoneNumber: request.PhoneNumber,
            PreferredLanguage: request.PreferredLanguage,
            IdentityReference: request.IdentityReference);

    public static AuthorisePointsCommand ToCommand(string loyaltyNumber, AuthorisePointsRequest request) =>
        new(
            LoyaltyNumber: loyaltyNumber,
            Points: request.Points,
            BasketId: request.BasketId);

    public static SettlePointsCommand ToCommand(string loyaltyNumber, SettlePointsRequest request) =>
        new(
            LoyaltyNumber: loyaltyNumber,
            RedemptionReference: request.RedemptionReference);

    public static ReversePointsCommand ToCommand(string loyaltyNumber, ReversePointsRequest request) =>
        new(
            LoyaltyNumber: loyaltyNumber,
            RedemptionReference: request.RedemptionReference,
            Reason: request.Reason);

    public static ReinstatePointsCommand ToCommand(string loyaltyNumber, ReinstatePointsRequest request) =>
        new(
            LoyaltyNumber: loyaltyNumber,
            Points: request.Points,
            BookingReference: request.BookingReference,
            Reason: request.Reason);

    // -------------------------------------------------------------------------
    // Domain entity → HTTP response
    // -------------------------------------------------------------------------

    public static CustomerResponse ToResponse(Domain.Entities.Customer customer) =>
        new()
        {
            CustomerId = customer.CustomerId,
            LoyaltyNumber = customer.LoyaltyNumber,
            IdentityReference = customer.IdentityReference,
            GivenName = customer.GivenName,
            Surname = customer.Surname,
            DateOfBirth = customer.DateOfBirth,
            Nationality = customer.Nationality,
            PreferredLanguage = customer.PreferredLanguage,
            PhoneNumber = customer.PhoneNumber,
            TierCode = customer.TierCode,
            PointsBalance = customer.PointsBalance,
            TierProgressPoints = customer.TierProgressPoints,
            IsActive = customer.IsActive,
            CreatedAt = customer.CreatedAt,
            UpdatedAt = customer.UpdatedAt
        };

    public static CreateCustomerResponse ToCreateResponse(Domain.Entities.Customer customer) =>
        new()
        {
            CustomerId = customer.CustomerId,
            LoyaltyNumber = customer.LoyaltyNumber,
            TierCode = customer.TierCode
        };

    public static TransactionsResponse ToResponse(string loyaltyNumber, IReadOnlyList<Domain.Entities.LoyaltyTransaction> transactions, int page, int pageSize, int totalCount) =>
        new()
        {
            LoyaltyNumber = loyaltyNumber,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Transactions = transactions.Select(ToTransactionResponse).ToList().AsReadOnly()
        };

    public static TransactionResponse ToTransactionResponse(Domain.Entities.LoyaltyTransaction transaction) =>
        new()
        {
            TransactionId = transaction.TransactionId,
            TransactionType = transaction.TransactionType,
            PointsDelta = transaction.PointsDelta,
            BalanceAfter = transaction.BalanceAfter,
            BookingReference = transaction.BookingReference,
            FlightNumber = transaction.FlightNumber,
            Description = transaction.Description,
            TransactionDate = transaction.TransactionDate
        };

    public static AuthorisePointsResponse ToAuthoriseResponse(Domain.Entities.LoyaltyTransaction transaction) =>
        new()
        {
            RedemptionReference = $"RDM-{transaction.TransactionDate:yyyyMMdd}-{transaction.TransactionId.ToString("N")[..6]}",
            PointsAuthorised = Math.Abs(transaction.PointsDelta),
            PointsHeld = Math.Abs(transaction.PointsDelta),
            AuthorisedAt = transaction.TransactionDate
        };

    public static SettlePointsResponse ToSettleResponse(Domain.Entities.LoyaltyTransaction transaction) =>
        new()
        {
            RedemptionReference = $"RDM-{transaction.TransactionDate:yyyyMMdd}-{transaction.TransactionId.ToString("N")[..6]}",
            PointsDeducted = Math.Abs(transaction.PointsDelta),
            NewPointsBalance = transaction.BalanceAfter,
            TransactionId = transaction.TransactionId,
            SettledAt = transaction.TransactionDate
        };

    public static ReversePointsResponse ToReverseResponse(Domain.Entities.LoyaltyTransaction transaction) =>
        new()
        {
            RedemptionReference = $"RDM-{transaction.TransactionDate:yyyyMMdd}-{transaction.TransactionId.ToString("N")[..6]}",
            PointsReleased = Math.Abs(transaction.PointsDelta),
            NewPointsBalance = transaction.BalanceAfter,
            ReversedAt = transaction.TransactionDate
        };

    public static ReinstatePointsResponse ToReinstateResponse(string loyaltyNumber, Domain.Entities.LoyaltyTransaction transaction) =>
        new()
        {
            LoyaltyNumber = loyaltyNumber,
            PointsReinstated = transaction.PointsDelta,
            NewPointsBalance = transaction.BalanceAfter,
            TransactionId = transaction.TransactionId,
            ReinstatedAt = transaction.TransactionDate
        };
}
