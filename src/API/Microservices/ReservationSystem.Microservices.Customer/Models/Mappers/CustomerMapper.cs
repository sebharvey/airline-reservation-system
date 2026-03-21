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
            PreferredLanguage: request.PreferredLanguage,
            TierCode: request.TierCode,
            IdentityReference: request.IdentityReference,
            DateOfBirth: request.DateOfBirth,
            Nationality: request.Nationality,
            PhoneNumber: request.PhoneNumber);

    public static UpdateCustomerCommand ToCommand(string loyaltyNumber, UpdateCustomerRequest request) =>
        new(
            LoyaltyNumber: loyaltyNumber,
            GivenName: request.GivenName,
            Surname: request.Surname,
            PreferredLanguage: request.PreferredLanguage,
            TierCode: request.TierCode,
            IdentityReference: request.IdentityReference,
            DateOfBirth: request.DateOfBirth,
            Nationality: request.Nationality,
            PhoneNumber: request.PhoneNumber);

    public static AuthorisePointsCommand ToCommand(string loyaltyNumber, AuthorisePointsRequest request) =>
        new(
            LoyaltyNumber: loyaltyNumber,
            Points: request.Points,
            BookingReference: request.BookingReference,
            FlightNumber: request.FlightNumber,
            Description: request.Description);

    public static SettlePointsCommand ToCommand(string loyaltyNumber, SettlePointsRequest request) =>
        new(
            LoyaltyNumber: loyaltyNumber,
            Points: request.Points,
            BookingReference: request.BookingReference,
            FlightNumber: request.FlightNumber,
            Description: request.Description);

    public static ReversePointsCommand ToCommand(string loyaltyNumber, ReversePointsRequest request) =>
        new(
            LoyaltyNumber: loyaltyNumber,
            Points: request.Points,
            BookingReference: request.BookingReference,
            FlightNumber: request.FlightNumber,
            Description: request.Description);

    public static ReinstatePointsCommand ToCommand(string loyaltyNumber, ReinstatePointsRequest request) =>
        new(
            LoyaltyNumber: loyaltyNumber,
            Points: request.Points,
            BookingReference: request.BookingReference,
            FlightNumber: request.FlightNumber,
            Description: request.Description);

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
            GivenName = customer.GivenName,
            Surname = customer.Surname,
            TierCode = customer.TierCode,
            PointsBalance = customer.PointsBalance,
            CreatedAt = customer.CreatedAt
        };

    public static TransactionsResponse ToResponse(string loyaltyNumber, IReadOnlyList<Domain.Entities.LoyaltyTransaction> transactions) =>
        new()
        {
            LoyaltyNumber = loyaltyNumber,
            Transactions = transactions.Select(ToTransactionResponse).ToList().AsReadOnly()
        };

    public static TransactionResponse ToTransactionResponse(Domain.Entities.LoyaltyTransaction transaction) =>
        new()
        {
            TransactionId = transaction.TransactionId,
            LoyaltyNumber = transaction.LoyaltyNumber,
            TransactionType = transaction.TransactionType,
            PointsDelta = transaction.PointsDelta,
            BalanceAfter = transaction.BalanceAfter,
            BookingReference = transaction.BookingReference,
            FlightNumber = transaction.FlightNumber,
            Description = transaction.Description,
            TransactionDate = transaction.TransactionDate,
            CreatedAt = transaction.CreatedAt
        };

    public static AuthorisePointsResponse ToAuthoriseResponse(Domain.Entities.LoyaltyTransaction transaction) =>
        new()
        {
            TransactionId = transaction.TransactionId,
            LoyaltyNumber = transaction.LoyaltyNumber,
            TransactionType = transaction.TransactionType,
            PointsDelta = transaction.PointsDelta,
            BalanceAfter = transaction.BalanceAfter,
            TransactionDate = transaction.TransactionDate
        };

    public static SettlePointsResponse ToSettleResponse(Domain.Entities.LoyaltyTransaction transaction) =>
        new()
        {
            TransactionId = transaction.TransactionId,
            LoyaltyNumber = transaction.LoyaltyNumber,
            TransactionType = transaction.TransactionType,
            PointsDelta = transaction.PointsDelta,
            BalanceAfter = transaction.BalanceAfter,
            TransactionDate = transaction.TransactionDate
        };

    public static ReversePointsResponse ToReverseResponse(Domain.Entities.LoyaltyTransaction transaction) =>
        new()
        {
            TransactionId = transaction.TransactionId,
            LoyaltyNumber = transaction.LoyaltyNumber,
            TransactionType = transaction.TransactionType,
            PointsDelta = transaction.PointsDelta,
            BalanceAfter = transaction.BalanceAfter,
            TransactionDate = transaction.TransactionDate
        };

    public static ReinstatePointsResponse ToReinstateResponse(Domain.Entities.LoyaltyTransaction transaction) =>
        new()
        {
            TransactionId = transaction.TransactionId,
            LoyaltyNumber = transaction.LoyaltyNumber,
            TransactionType = transaction.TransactionType,
            PointsDelta = transaction.PointsDelta,
            BalanceAfter = transaction.BalanceAfter,
            TransactionDate = transaction.TransactionDate
        };
}
