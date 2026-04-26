using System.Net;
using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace ReservationSystem.Tests.IntegrationTests.NdcOrderCreate;

/// <summary>
/// Integration tests for the NDC 21.3 OrderCreate endpoint (POST /v1/ndc/OrderCreate).
///
/// Tests run in priority order. T01–T04 form a dependency chain that captures a live
/// OfferId from AirShopping and carries it through OfferPrice into OrderCreate.
/// T05–T10 are standalone validation/error-path tests that do not depend on earlier state.
///
/// Prerequisites:
///   RETAIL_API_BASE_URL  — base URL of the Retail API (defaults to the deployed Azure URL)
///   RETAIL_API_HOST_KEY  — optional Azure Functions host key
/// </summary>
[TestCaseOrderer(
    "ReservationSystem.Tests.IntegrationTests.NdcOrderCreate.NdcOrderCreatePriorityOrderer",
    "ReservationSystem.Tests")]
public class NdcOrderCreateIntegrationTests : IAsyncLifetime
{
    private static readonly string BaseUrl =
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RETAIL_API_BASE_URL"))
            ? "https://reservation-system-db-api-retail-aqasakbxcje0a6eh.uksouth-01.azurewebsites.net"
            : Environment.GetEnvironmentVariable("RETAIL_API_BASE_URL")!;

    private static readonly string? HostKey =
        Environment.GetEnvironmentVariable("RETAIL_API_HOST_KEY");

    private static readonly XNamespace RsNs =
        "http://www.iata.org/IATA/2015/00/2021.3/IATA_OrderCreateRS";

    private static readonly XNamespace AirShoppingRsNs =
        "http://www.iata.org/IATA/2015/00/2021.3/IATA_AirShoppingRS";

    // Shared state threaded through the ordered tests.
    private static string? _shoppingResponseId;
    private static string? _offerId;
    private static string? _bookingReference;
    private static string? _orderId;

    private readonly HttpClient _client;

    public NdcOrderCreateIntegrationTests()
    {
        _client = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        _client.Timeout = TimeSpan.FromSeconds(60);

        if (!string.IsNullOrEmpty(HostKey))
            _client.DefaultRequestHeaders.Add("x-functions-key", HostKey);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    // ── T01 — Obtain a live OfferId via AirShopping ───────────────────────────

    [Fact, TestPriority(1)]
    public async Task T01_OrderCreate_Setup_ObtainOfferIdFromAirShopping()
    {
        var airShoppingXml = BuildAirShoppingRq("LHR", "JFK", "2026-07-15", [("ADT", 1)]);
        var content = new StringContent(airShoppingXml, Encoding.UTF8, "application/xml");
        var response = await _client.PostAsync("/api/v1/ndc/AirShopping", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var root = XDocument.Parse(body).Root!;

        _shoppingResponseId = root
            .Element(AirShoppingRsNs + "ShoppingResponseID")
            ?.Element(AirShoppingRsNs + "ResponseID")?.Value;

        _offerId = root
            .Element(AirShoppingRsNs + "OffersGroup")
            ?.Element(AirShoppingRsNs + "AirlineOffers")
            ?.Element(AirShoppingRsNs + "Offer")
            ?.Element(AirShoppingRsNs + "OfferID")?.Value;

        Skip.If(string.IsNullOrWhiteSpace(_offerId),
            "No offers returned by AirShopping — no inventory for LHR→JFK on 2026-07-15.");
    }

    // ── T02 — Valid OrderCreateRQ returns 201 with well-formed RS ─────────────

    [SkippableFact, TestPriority(2)]
    public async Task T02_OrderCreate_ValidRequest_Returns201WithOrderCreateRS()
    {
        Skip.If(_offerId is null, "T01 did not produce an OfferId.");

        var requestXml = BuildOrderCreateRq(
            offerId           : _offerId!,
            shoppingResponseId: _shoppingResponseId,
            passengers        : [("PAX1", "ADT", "John", "Smith", "1985-05-15", "M", "john.smith@example.com", "+44 7700 900123")],
            card              : ("John Smith", "4111111111111111", "VI", "12", "2028", "123"));

        var response = await PostOrderCreateXmlAsync(requestXml);

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "a successful OrderCreate must return 201 Created");
        response.Content.Headers.ContentType?.MediaType.Should().StartWith("application/xml");

        var body = await response.Content.ReadAsStringAsync();
        var doc  = XDocument.Parse(body);
        var root = doc.Root;

        root.Should().NotBeNull();
        root!.Name.LocalName.Should().Be("IATA_OrderCreateRS");

        var responseEl = root.Element(RsNs + "Response");
        responseEl.Should().NotBeNull("Response element must be present");

        var orderEl = responseEl!.Element(RsNs + "Order");
        orderEl.Should().NotBeNull("Response/Order must be present");

        _orderId = orderEl!.Element(RsNs + "OrderID")?.Value;
        _orderId.Should().NotBeNullOrWhiteSpace("OrderID must be present and non-empty");

        var bookingRefEl = orderEl.Element(RsNs + "BookingRef")?.Element(RsNs + "ID");
        bookingRefEl.Should().NotBeNull("BookingRef/ID must be present");
        _bookingReference = bookingRefEl!.Value;
        _bookingReference.Should().NotBeNullOrWhiteSpace();
    }

    // ── T03 — Order contains required structural elements ─────────────────────

    [SkippableFact, TestPriority(3)]
    public async Task T03_OrderCreate_Response_ContainsRequiredStructure()
    {
        Skip.If(_offerId is null, "T01 did not produce an OfferId.");
        Skip.If(_bookingReference is null, "T02 did not complete successfully.");

        // Re-issue an OrderCreate with a fresh offer to inspect the full structure.
        // (The prior OfferId was consumed; get a new one.)
        var freshOfferId = await FetchFirstOfferIdAsync();
        Skip.If(freshOfferId is null, "No inventory available for structural test.");

        var requestXml = BuildOrderCreateRq(
            offerId           : freshOfferId!,
            shoppingResponseId: null,
            passengers        : [("PAX1", "ADT", "Jane", "Doe", "1990-08-20", "F", "jane.doe@example.com", null)],
            card              : ("Jane Doe", "4111111111111111", "VI", "06", "2029", "456"));

        var response = await PostOrderCreateXmlAsync(requestXml);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var root = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!;
        var order = root.Element(RsNs + "Response")?.Element(RsNs + "Order");
        order.Should().NotBeNull();

        // StatusCode must be ISSUED.
        order!.Element(RsNs + "StatusCode")?.Value.Should().Be("ISSUED");

        // TotalAmount must have CurCode and a positive decimal value.
        var totalAmountEl = order.Element(RsNs + "TotalAmount");
        totalAmountEl.Should().NotBeNull("TotalAmount must be present");
        totalAmountEl!.Attribute("CurCode")?.Value.Should().NotBeNullOrWhiteSpace();
        decimal.TryParse(totalAmountEl.Value, out var total).Should().BeTrue();
        total.Should().BeGreaterThan(0);

        // BookingRef must have carrier code AX and a non-empty PNR.
        var bookingRef = order.Element(RsNs + "BookingRef");
        bookingRef.Should().NotBeNull();
        bookingRef!.Element(RsNs + "BookingEntity")
            ?.Element(RsNs + "Carrier")
            ?.Element(RsNs + "AirlineDesigCode")?.Value.Should().Be("AX");
        bookingRef.Element(RsNs + "ID")?.Value.Should().NotBeNullOrWhiteSpace();

        // At least one OrderItem.
        var orderItems = order.Elements(RsNs + "OrderItem").ToList();
        orderItems.Should().NotBeEmpty("at least one OrderItem must be present");

        // OrderItem must have Price/TotalAmount.
        var firstItem = orderItems[0];
        firstItem.Element(RsNs + "OrderItemID")?.Value.Should().NotBeNullOrWhiteSpace();
        firstItem.Element(RsNs + "StatusCode")?.Value.Should().Be("PAYMENT_DONE");
        var itemTotal = firstItem.Element(RsNs + "Price")?.Element(RsNs + "TotalAmount");
        itemTotal.Should().NotBeNull("OrderItem/Price/TotalAmount must be present");
    }

    // ── T04 — DataLists are populated correctly ────────────────────────────────

    [SkippableFact, TestPriority(4)]
    public async Task T04_OrderCreate_DataLists_ContainsFlightAndPassengerData()
    {
        Skip.If(_offerId is null, "T01 did not produce an OfferId.");

        var freshOfferId = await FetchFirstOfferIdAsync();
        Skip.If(freshOfferId is null, "No inventory available.");

        var requestXml = BuildOrderCreateRq(
            offerId           : freshOfferId!,
            shoppingResponseId: null,
            passengers        : [("PAX1", "ADT", "James", "Brown", "1978-03-10", "M", "james.brown@example.com", null)],
            card              : ("James Brown", "4111111111111111", "VI", "09", "2027", "789"));

        var response = await PostOrderCreateXmlAsync(requestXml);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var root = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!;
        var dataLists = root.Element(RsNs + "Response")?.Element(RsNs + "DataLists");
        dataLists.Should().NotBeNull("DataLists must be present");

        // FlightSegmentList.
        var segList = dataLists!.Element(RsNs + "FlightSegmentList");
        segList.Should().NotBeNull("FlightSegmentList must be present");
        var seg = segList!.Element(RsNs + "FlightSegment");
        seg.Should().NotBeNull("at least one FlightSegment must be present");
        seg!.Element(RsNs + "SegmentKey")?.Value.Should().StartWith("SEG");
        seg.Element(RsNs + "Departure")?.Element(RsNs + "AirportCode")?.Value.Should().Be("LHR");
        seg.Element(RsNs + "Arrival")?.Element(RsNs + "AirportCode")?.Value.Should().Be("JFK");
        seg.Element(RsNs + "MarketingCarrier")?.Element(RsNs + "AirlineID")?.Value.Should().Be("AX");
        seg.Element(RsNs + "Equipment")?.Element(RsNs + "AircraftCode")?.Value.Should().HaveLength(3,
            "AircraftCode must be IATA 3-char code");

        // OriginDestinationList.
        var odList = dataLists.Element(RsNs + "OriginDestinationList");
        odList.Should().NotBeNull("OriginDestinationList must be present");
        var od = odList!.Element(RsNs + "OriginDestination");
        od.Should().NotBeNull();
        od!.Element(RsNs + "DepartureCode")?.Value.Should().Be("LHR");
        od.Element(RsNs + "ArrivalCode")?.Value.Should().Be("JFK");

        // PaxList must reflect request passengers.
        var paxList = dataLists.Element(RsNs + "PaxList");
        paxList.Should().NotBeNull("PaxList must be present");
        var pax = paxList!.Element(RsNs + "Pax");
        pax.Should().NotBeNull();
        pax!.Element(RsNs + "PaxID")?.Value.Should().Be("PAX1");
        pax.Element(RsNs + "PTC")?.Value.Should().Be("ADT");
        pax.Element(RsNs + "Individual")?.Element(RsNs + "Surname")?.Value.Should().Be("BROWN");
    }

    // ── T05 — Document element declares NDC version 21.3 ─────────────────────

    [Fact, TestPriority(5)]
    public async Task T05_OrderCreate_Response_DocumentVersionIs213()
    {
        // Use a well-formed but non-existent GUID so we get a structured error response.
        var xml = BuildOrderCreateRq(
            offerId           : Guid.NewGuid().ToString(),
            shoppingResponseId: null,
            passengers        : [("PAX1", "ADT", "Test", "User", null, null, null, null)],
            card              : ("Test User", "4111111111111111", "VI", "01", "2030", "000"));

        var response = await PostOrderCreateXmlAsync(xml);

        // Offer will not be found — but the response must still be valid XML with version.
        var body = await response.Content.ReadAsStringAsync();
        var root = XDocument.Parse(body).Root!;

        root.Name.LocalName.Should().Be("IATA_OrderCreateRS");

        var version = root.Element(RsNs + "Document")?.Element(RsNs + "ReferenceVersion")?.Value;
        version.Should().Be("21.3", "the response must declare NDC schema version 21.3");
    }

    // ── T06 — Missing Query/OrderItems returns 400 ────────────────────────────

    [Fact, TestPriority(6)]
    public async Task T06_OrderCreate_MissingOrderItems_Returns400WithErrorElement()
    {
        const string malformedXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <IATA_OrderCreateRQ xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_OrderCreateRQ">
              <Document><ReferenceVersion>21.3</ReferenceVersion></Document>
              <Query>
                <DataLists>
                  <PaxList>
                    <Pax>
                      <PaxID>PAX1</PaxID>
                      <PTC>ADT</PTC>
                      <Individual><GivenName>Test</GivenName><Surname>User</Surname></Individual>
                    </Pax>
                  </PaxList>
                </DataLists>
              </Query>
            </IATA_OrderCreateRQ>
            """;

        var response = await PostOrderCreateXmlAsync(malformedXml);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var root = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!;
        var error = root.Element(RsNs + "Errors")?.Element(RsNs + "Error");
        error.Should().NotBeNull("a missing OrderItems must produce an Errors/Error element");
        error!.Element(RsNs + "Code")?.Value.Should().Be("ERR_PARSE");
    }

    // ── T07 — Missing PaxList returns 400 ─────────────────────────────────────

    [Fact, TestPriority(7)]
    public async Task T07_OrderCreate_MissingPaxList_Returns400()
    {
        const string malformedXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <IATA_OrderCreateRQ xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_OrderCreateRQ">
              <Document><ReferenceVersion>21.3</ReferenceVersion></Document>
              <Query>
                <OrderItems>
                  <OfferItem>
                    <OfferRefID>00000000-0000-0000-0000-000000000000</OfferRefID>
                  </OfferItem>
                </OrderItems>
                <DataLists />
              </Query>
            </IATA_OrderCreateRQ>
            """;

        var response = await PostOrderCreateXmlAsync(malformedXml);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var root = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!;
        root.Element(RsNs + "Errors")?.Element(RsNs + "Error")
            .Should().NotBeNull("a missing PaxList must produce an Errors/Error element");
    }

    // ── T08 — Invalid OfferRefID format returns 400 ───────────────────────────

    [Fact, TestPriority(8)]
    public async Task T08_OrderCreate_InvalidOfferRefId_Returns400()
    {
        const string invalidXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <IATA_OrderCreateRQ xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_OrderCreateRQ">
              <Document><ReferenceVersion>21.3</ReferenceVersion></Document>
              <Query>
                <OrderItems>
                  <OfferItem>
                    <OfferRefID>NOT-A-VALID-GUID</OfferRefID>
                  </OfferItem>
                </OrderItems>
                <DataLists>
                  <PaxList>
                    <Pax>
                      <PaxID>PAX1</PaxID>
                      <PTC>ADT</PTC>
                      <Individual><GivenName>Test</GivenName><Surname>User</Surname></Individual>
                    </Pax>
                  </PaxList>
                </DataLists>
              </Query>
            </IATA_OrderCreateRQ>
            """;

        var response = await PostOrderCreateXmlAsync(invalidXml);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── T09 — Invalid XML body returns 400 ───────────────────────────────────

    [Fact, TestPriority(9)]
    public async Task T09_OrderCreate_InvalidXml_Returns400()
    {
        var response = await PostOrderCreateXmlAsync("not xml << >");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── T10 — Empty body returns 400 ─────────────────────────────────────────

    [Fact, TestPriority(10)]
    public async Task T10_OrderCreate_EmptyBody_Returns400()
    {
        var content = new StringContent(string.Empty, Encoding.UTF8, "application/xml");
        var response = await _client.PostAsync("/api/v1/ndc/OrderCreate", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── T11 — Non-existent OfferRefID returns 404 ────────────────────────────

    [Fact, TestPriority(11)]
    public async Task T11_OrderCreate_OfferNotFound_Returns404()
    {
        var xml = BuildOrderCreateRq(
            offerId           : Guid.NewGuid().ToString(),
            shoppingResponseId: null,
            passengers        : [("PAX1", "ADT", "Nobody", "Known", null, null, null, null)],
            card              : ("Nobody Known", "4111111111111111", "VI", "01", "2030", "000"));

        var response = await PostOrderCreateXmlAsync(xml);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var root = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!;
        var code = root.Element(RsNs + "Errors")?.Element(RsNs + "Error")
            ?.Element(RsNs + "Code")?.Value;
        code.Should().Be("ERR_OFFER_NOT_FOUND");
    }

    // ── T12 — Missing passenger name fields returns 400 ──────────────────────

    [Fact, TestPriority(12)]
    public async Task T12_OrderCreate_MissingPassengerName_Returns400()
    {
        const string missingNameXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <IATA_OrderCreateRQ xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_OrderCreateRQ">
              <Document><ReferenceVersion>21.3</ReferenceVersion></Document>
              <Query>
                <OrderItems>
                  <OfferItem>
                    <OfferRefID>00000000-0000-0000-0000-000000000001</OfferRefID>
                  </OfferItem>
                </OrderItems>
                <DataLists>
                  <PaxList>
                    <Pax>
                      <PaxID>PAX1</PaxID>
                      <PTC>ADT</PTC>
                      <Individual></Individual>
                    </Pax>
                  </PaxList>
                </DataLists>
              </Query>
            </IATA_OrderCreateRQ>
            """;

        var response = await PostOrderCreateXmlAsync(missingNameXml);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Task<HttpResponseMessage> PostOrderCreateXmlAsync(string xml)
    {
        var content = new StringContent(xml, Encoding.UTF8, "application/xml");
        return _client.PostAsync("/api/v1/ndc/OrderCreate", content);
    }

    private async Task<string?> FetchFirstOfferIdAsync()
    {
        var airShoppingXml = BuildAirShoppingRq("LHR", "JFK", "2026-07-15", [("ADT", 1)]);
        var content = new StringContent(airShoppingXml, Encoding.UTF8, "application/xml");
        var response = await _client.PostAsync("/api/v1/ndc/AirShopping", content);
        if (!response.IsSuccessStatusCode) return null;

        var root = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root;
        return root
            ?.Element(AirShoppingRsNs + "OffersGroup")
            ?.Element(AirShoppingRsNs + "AirlineOffers")
            ?.Element(AirShoppingRsNs + "Offer")
            ?.Element(AirShoppingRsNs + "OfferID")?.Value;
    }

    private static string BuildOrderCreateRq(
        string offerId,
        string? shoppingResponseId,
        IEnumerable<(string PaxId, string Ptc, string GivenName, string Surname, string? Dob, string? Gender, string? Email, string? Phone)> passengers,
        (string CardholderName, string CardNumber, string CardTypeCode, string Month, string Year, string Cvv)? card)
    {
        var paxListXml = string.Join(Environment.NewLine, passengers.Select(p =>
        {
            var dobXml      = p.Dob    is not null ? $"<Birthdate>{p.Dob}</Birthdate>" : string.Empty;
            var genderXml   = p.Gender is not null ? $"<GenderCode>{p.Gender}</GenderCode>" : string.Empty;
            var contactRef  = $"CI-{p.PaxId}";
            return $"""
                    <Pax>
                      <PaxID>{p.PaxId}</PaxID>
                      <PTC>{p.Ptc}</PTC>
                      <Individual>
                        {dobXml}
                        {genderXml}
                        <GivenName>{p.GivenName}</GivenName>
                        <Surname>{p.Surname}</Surname>
                      </Individual>
                      <ContactInfoRefID>{contactRef}</ContactInfoRefID>
                    </Pax>
                """;
        }));

        var contactListXml = string.Join(Environment.NewLine, passengers.Select(p =>
        {
            var contactRef = $"CI-{p.PaxId}";
            var emailXml   = p.Email is not null
                ? $"<EmailAddress><EmailAddressText>{p.Email}</EmailAddressText></EmailAddress>"
                : string.Empty;
            var phoneXml   = p.Phone is not null
                ? $"<Phone><PhoneNumber>{p.Phone}</PhoneNumber></Phone>"
                : string.Empty;

            if (string.IsNullOrEmpty(emailXml) && string.IsNullOrEmpty(phoneXml))
                return string.Empty;

            return $"""
                    <ContactInfo>
                      <ContactInfoID>{contactRef}</ContactInfoID>
                      {emailXml}
                      {phoneXml}
                    </ContactInfo>
                """;
        }));

        var shoppingResponseXml = shoppingResponseId is not null
            ? $"""
                  <ShoppingResponse>
                    <Owner>AX</Owner>
                    <ResponseID>{shoppingResponseId}</ResponseID>
                  </ShoppingResponse>
              """
            : string.Empty;

        var paymentXml = card is not null
            ? $"""
                <Payments>
                  <Payment>
                    <Method>
                      <PaymentCard>
                        <CardHolderName>{card.Value.CardholderName}</CardHolderName>
                        <CardNumber>
                          <Applicability>FullCard</Applicability>
                          <PlainCardNumber>{card.Value.CardNumber}</PlainCardNumber>
                        </CardNumber>
                        <CardTypeCode>{card.Value.CardTypeCode}</CardTypeCode>
                        <Expiry>
                          <Month>{card.Value.Month}</Month>
                          <Year>{card.Value.Year}</Year>
                        </Expiry>
                        <SeriesCode>
                          <Applicability>CVV</Applicability>
                          <Value>{card.Value.Cvv}</Value>
                        </SeriesCode>
                      </PaymentCard>
                    </Method>
                  </Payment>
                </Payments>
              """
            : string.Empty;

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <IATA_OrderCreateRQ xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_OrderCreateRQ">
              <Document>
                <Name>Apex Air NDC OrderCreate Test</Name>
                <ReferenceVersion>21.3</ReferenceVersion>
              </Document>
              <Party>
                <Sender>
                  <TravelAgencySender>
                    <AgencyID>TEST001</AgencyID>
                    <Name>Test OTA</Name>
                  </TravelAgencySender>
                </Sender>
                <Recipient>
                  <AirlineRecipient>
                    <AirlineDesigCode>AX</AirlineDesigCode>
                  </AirlineRecipient>
                </Recipient>
              </Party>
              <Query>
                <OrderItems>
                  {shoppingResponseXml}
                  <OfferItem>
                    <OfferItemRefID>ITEM-{offerId.Replace("-", "")}</OfferItemRefID>
                    <OfferRefID>{offerId}</OfferRefID>
                  </OfferItem>
                </OrderItems>
                <DataLists>
                  <PaxList>
                    {paxListXml}
                  </PaxList>
                  <ContactInfoList>
                    {contactListXml}
                  </ContactInfoList>
                </DataLists>
                {paymentXml}
              </Query>
            </IATA_OrderCreateRQ>
            """;
    }

    private static string BuildAirShoppingRq(
        string origin,
        string destination,
        string departureDate,
        IEnumerable<(string Ptc, int Qty)> passengers)
    {
        var travelersXml = string.Join(Environment.NewLine,
            passengers.Select(p => $"""
                    <Traveler>
                      <AnonymousTraveler>
                        <PTC>{p.Ptc}</PTC>
                        <Quantity>{p.Qty}</Quantity>
                      </AnonymousTraveler>
                    </Traveler>
                """));

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <IATA_AirShoppingRQ xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_AirShoppingRQ">
              <Document>
                <Name>Apex Air NDC Test</Name>
                <ReferenceVersion>21.3</ReferenceVersion>
              </Document>
              <Party>
                <Sender>
                  <TravelAgencySender>
                    <AgencyID>TEST001</AgencyID>
                    <Name>Test OTA</Name>
                  </TravelAgencySender>
                </Sender>
                <Recipient>
                  <AirlineRecipient>
                    <AirlineDesigCode>AX</AirlineDesigCode>
                  </AirlineRecipient>
                </Recipient>
              </Party>
              <Travelers>
                {travelersXml}
              </Travelers>
              <CoreQuery>
                <OriginDestinations>
                  <OriginDestination>
                    <Departure>
                      <AirportCode>{origin}</AirportCode>
                      <Date>{departureDate}</Date>
                    </Departure>
                    <Arrival>
                      <AirportCode>{destination}</AirportCode>
                    </Arrival>
                  </OriginDestination>
                </OriginDestinations>
              </CoreQuery>
            </IATA_AirShoppingRQ>
            """;
    }
}

#region Test Ordering Infrastructure

[AttributeUsage(AttributeTargets.Method)]
public sealed class NdcOrderCreatePriorityAttribute(int priority) : Attribute
{
    public int Priority { get; } = priority;
}

// Re-use TestPriorityAttribute naming convention from AirShopping tests via a local alias.
[AttributeUsage(AttributeTargets.Method)]
file sealed class TestPriorityAttribute(int priority) : Attribute
{
    public int Priority { get; } = priority;
}

public sealed class NdcOrderCreatePriorityOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
    {
        var sorted = new SortedDictionary<int, List<TTestCase>>();

        foreach (var testCase in testCases)
        {
            var priority = testCase.TestMethod.Method
                .GetCustomAttributes(typeof(TestPriorityAttribute).AssemblyQualifiedName!, false)
                .FirstOrDefault()
                ?.GetNamedArgument<int>("Priority") ?? 0;

            if (!sorted.TryGetValue(priority, out var list))
                sorted[priority] = list = [];

            list.Add(testCase);
        }

        return sorted.Values.SelectMany(v => v);
    }
}

#endregion
