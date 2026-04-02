using ReservationSystem.Microservices.Customer.Application.AddPoints;
using ReservationSystem.Microservices.Customer.Application.CreateCustomer;
using ReservationSystem.Microservices.Customer.Application.TransferPoints;
using ReservationSystem.Microservices.Customer.Application.UpdateCustomer;
using ReservationSystem.Microservices.Customer.Application.UpdatePreferences;
using ReservationSystem.Microservices.Customer.Application.AuthorisePoints;
using ReservationSystem.Microservices.Customer.Application.SettlePoints;
using ReservationSystem.Microservices.Customer.Application.ReversePoints;
using ReservationSystem.Microservices.Customer.Application.ReinstatePoints;
using ReservationSystem.Microservices.Customer.Domain.Entities;
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

    public static CreateCustomerCommand ToCommand(CreateCustomerRequest request) =>
        new(
            GivenName: request.GivenName,
            Surname: request.Surname,
            DateOfBirth: request.DateOfBirth,
            PreferredLanguage: request.PreferredLanguage,
            IdentityId: request.IdentityId,
            PhoneNumber: request.PhoneNumber,
            Nationality: request.Nationality);

    public static UpdateCustomerCommand ToCommand(string loyaltyNumber, UpdateCustomerRequest request) =>
        new(
            LoyaltyNumber: loyaltyNumber,
            GivenName: request.GivenName,
            Surname: request.Surname,
            DateOfBirth: request.DateOfBirth,
            Gender: request.Gender,
            Nationality: request.Nationality,
            PhoneNumber: request.PhoneNumber,
            PreferredLanguage: request.PreferredLanguage,
            AddressLine1: request.AddressLine1,
            AddressLine2: request.AddressLine2,
            City: request.City,
            StateOrRegion: request.StateOrRegion,
            PostalCode: request.PostalCode,
            CountryCode: request.CountryCode,
            PassportNumber: request.PassportNumber,
            PassportIssueDate: request.PassportIssueDate,
            PassportIssuer: request.PassportIssuer,
            PassportExpiryDate: request.PassportExpiryDate,
            KnownTravellerNumber: request.KnownTravellerNumber,
            IdentityId: request.IdentityId,
            TierCode: request.TierCode,
            IsActive: request.IsActive);

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

    public static AddPointsCommand ToCommand(string loyaltyNumber, AddPointsRequest request) =>
        new(
            LoyaltyNumber: loyaltyNumber,
            Points: request.Points,
            TransactionType: request.TransactionType,
            Description: request.Description);

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
            IdentityId = customer.IdentityId,
            GivenName = customer.GivenName,
            Surname = customer.Surname,
            DateOfBirth = customer.DateOfBirth,
            Gender = customer.Gender,
            Nationality = customer.Nationality,
            PreferredLanguage = customer.PreferredLanguage,
            PhoneNumber = customer.PhoneNumber,
            AddressLine1 = customer.AddressLine1,
            AddressLine2 = customer.AddressLine2,
            City = customer.City,
            StateOrRegion = customer.StateOrRegion,
            PostalCode = customer.PostalCode,
            CountryCode = customer.CountryCode,
            PassportNumber = customer.PassportNumber,
            PassportIssueDate = customer.PassportIssueDate,
            PassportIssuer = customer.PassportIssuer,
            PassportExpiryDate = customer.PassportExpiryDate,
            KnownTravellerNumber = customer.KnownTravellerNumber,
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
            RedemptionReference = transaction.TransactionId.ToString(),
            PointsAuthorised = Math.Abs(transaction.PointsDelta),
            PointsHeld = Math.Abs(transaction.PointsDelta),
            AuthorisedAt = transaction.TransactionDate
        };

    public static SettlePointsResponse ToSettleResponse(Domain.Entities.LoyaltyTransaction transaction) =>
        new()
        {
            RedemptionReference = transaction.TransactionId.ToString(),
            PointsDeducted = Math.Abs(transaction.PointsDelta),
            NewPointsBalance = transaction.BalanceAfter,
            TransactionId = transaction.TransactionId,
            SettledAt = transaction.TransactionDate
        };

    public static ReversePointsResponse ToReverseResponse(Domain.Entities.LoyaltyTransaction transaction) =>
        new()
        {
            RedemptionReference = transaction.TransactionId.ToString(),
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

    public static AddPointsResponse ToAddPointsResponse(string loyaltyNumber, Domain.Entities.LoyaltyTransaction transaction) =>
        new()
        {
            LoyaltyNumber = loyaltyNumber,
            PointsAdded = transaction.PointsDelta,
            NewPointsBalance = transaction.BalanceAfter,
            TransactionId = transaction.TransactionId,
            AddedAt = transaction.TransactionDate
        };

    public static CustomerPreferencesResponse ToPreferencesResponse(CustomerPreferences preferences) =>
        new()
        {
            CustomerId = preferences.CustomerId,
            MarketingEnabled = preferences.MarketingEnabled,
            AnalyticsEnabled = preferences.AnalyticsEnabled,
            FunctionalEnabled = preferences.FunctionalEnabled,
            AppNotificationsEnabled = preferences.AppNotificationsEnabled
        };

    public static UpdatePreferencesCommand ToCommand(string loyaltyNumber, UpdatePreferencesRequest request) =>
        new(
            LoyaltyNumber: loyaltyNumber,
            MarketingEnabled: request.MarketingEnabled,
            AnalyticsEnabled: request.AnalyticsEnabled,
            FunctionalEnabled: request.FunctionalEnabled,
            AppNotificationsEnabled: request.AppNotificationsEnabled);

    public static TransferPointsCommand ToCommand(string senderLoyaltyNumber, TransferPointsRequest request) =>
        new(
            SenderLoyaltyNumber: senderLoyaltyNumber,
            RecipientLoyaltyNumber: request.RecipientLoyaltyNumber,
            Points: request.Points);
}
