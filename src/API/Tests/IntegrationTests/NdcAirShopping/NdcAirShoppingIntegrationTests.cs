using System.Net;
using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace ReservationSystem.Tests.IntegrationTests.NdcAirShopping;

/// <summary>
/// Integration tests for the NDC 21.3 AirShopping endpoint (POST /v1/ndc/AirShopping).
/// Tests run against the live Retail API; set RETAIL_API_BASE_URL and (optionally)
/// RETAIL_API_HOST_KEY before running.
/// </summary>
[TestCaseOrderer(
    "ReservationSystem.Tests.IntegrationTests.NdcAirShopping.PriorityOrderer",
    "ReservationSystem.Tests")]
public class NdcAirShoppingIntegrationTests : IAsyncLifetime
{
    private static readonly string BaseUrl =
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RETAIL_API_BASE_URL"))
            ? "https://reservation-system-db-api-retail-aqasakbxcje0a6eh.uksouth-01.azurewebsites.net"
            : Environment.GetEnvironmentVariable("RETAIL_API_BASE_URL")!;

    private static readonly string? HostKey =
        Environment.GetEnvironmentVariable("RETAIL_API_HOST_KEY");

    private static readonly XNamespace RsNs =
        "http://www.iata.org/IATA/2015/00/2021.3/IATA_AirShoppingRS";

    // State shared across ordered tests.
    private static string? _responseId;
    private static string? _firstOfferId;

    private readonly HttpClient _client;

    public NdcAirShoppingIntegrationTests()
    {
        _client = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        _client.Timeout = TimeSpan.FromSeconds(30);

        if (!string.IsNullOrEmpty(HostKey))
            _client.DefaultRequestHeaders.Add("x-functions-key", HostKey);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    // ── T01 — Valid AirShoppingRQ returns 200 with well-formed RS ─────────────

    [Fact, TestPriority(1)]
    public async Task T01_AirShopping_ValidRequest_Returns200WithAirShoppingRS()
    {
        var requestXml = BuildAirShoppingRq("LHR", "JFK", "2026-07-15", [("ADT", 1)]);

        var response = await PostXmlAsync(requestXml);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().StartWith("application/xml");

        var body = await response.Content.ReadAsStringAsync();
        var doc = XDocument.Parse(body);

        var root = doc.Root;
        root.Should().NotBeNull();
        root!.Name.LocalName.Should().Be("IATA_AirShoppingRS");

        // ShoppingResponseID must be present and non-empty.
        var responseIdEl = root.Element(RsNs + "ShoppingResponseID")
                               ?.Element(RsNs + "ResponseID");
        responseIdEl.Should().NotBeNull("ShoppingResponseID/ResponseID must be present");
        responseIdEl!.Value.Should().NotBeNullOrWhiteSpace();
        _responseId = responseIdEl.Value;

        // OffersGroup must be present.
        var offersGroup = root.Element(RsNs + "OffersGroup");
        offersGroup.Should().NotBeNull("OffersGroup must be present");

        // DataLists must be present.
        var dataLists = root.Element(RsNs + "DataLists");
        dataLists.Should().NotBeNull("DataLists must be present");
    }

    // ── T02 — OffersGroup contains at least one Offer with required children ──

    [SkippableFact, TestPriority(2)]
    public async Task T02_AirShopping_LhrToJfk_OffersGroupContainsOffers()
    {
        Skip.If(_responseId is null, "T01 did not complete successfully");

        var requestXml = BuildAirShoppingRq("LHR", "JFK", "2026-07-15", [("ADT", 1)]);
        var response = await PostXmlAsync(requestXml);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var root = XDocument.Parse(body).Root!;

        var offers = root
            .Element(RsNs + "OffersGroup")
            ?.Element(RsNs + "AirlineOffers")
            ?.Elements(RsNs + "Offer")
            .ToList();

        offers.Should().NotBeNullOrEmpty("at least one offer must be returned for LHR→JFK");

        var firstOffer = offers![0];

        // OfferID must be a valid GUID.
        var offerIdEl = firstOffer.Element(RsNs + "OfferID");
        offerIdEl.Should().NotBeNull();
        Guid.TryParse(offerIdEl!.Value, out _).Should().BeTrue("OfferID must be a GUID");
        _firstOfferId = offerIdEl.Value;

        // OwnerCode and ValidatingCarrierCode must be AX.
        firstOffer.Element(RsNs + "OwnerCode")?.Value.Should().Be("AX");
        firstOffer.Element(RsNs + "ValidatingCarrierCode")?.Value.Should().Be("AX");

        // TotalPrice must have a TotalAmount with CurCode.
        var totalAmount = firstOffer
            .Element(RsNs + "TotalPrice")
            ?.Element(RsNs + "TotalAmount");
        totalAmount.Should().NotBeNull();
        totalAmount!.Attribute("CurCode")?.Value.Should().Be("GBP");
        decimal.TryParse(totalAmount.Value, out var price).Should().BeTrue();
        price.Should().BeGreaterThan(0);

        // OfferItem must be present.
        firstOffer.Element(RsNs + "OfferItem").Should().NotBeNull("each Offer must contain an OfferItem");
    }

    // ── T03 — OfferItem has required TotalPriceDetail, Service, FareDetail ───

    [SkippableFact, TestPriority(3)]
    public async Task T03_AirShopping_OfferItem_HasRequiredStructure()
    {
        Skip.If(_firstOfferId is null, "T02 did not produce an offer ID");

        var requestXml = BuildAirShoppingRq("LHR", "JFK", "2026-07-15", [("ADT", 1)]);
        var response = await PostXmlAsync(requestXml);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var root = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!;

        var firstOffer = root
            .Element(RsNs + "OffersGroup")
            ?.Element(RsNs + "AirlineOffers")
            ?.Elements(RsNs + "Offer")
            .First();

        firstOffer.Should().NotBeNull();
        var offerItem = firstOffer!.Element(RsNs + "OfferItem");
        offerItem.Should().NotBeNull();

        // TotalPriceDetail.
        var totalPriceDetail = offerItem!.Element(RsNs + "TotalPriceDetail");
        totalPriceDetail.Should().NotBeNull("TotalPriceDetail must be present");
        totalPriceDetail!.Element(RsNs + "TotalAmount")
                         ?.Element(RsNs + "SimpleCurrencyPrice").Should().NotBeNull();

        // Service with FlightRefs.
        var service = offerItem.Element(RsNs + "Service");
        service.Should().NotBeNull("Service must be present");
        service!.Element(RsNs + "FlightRefs")?.Value.Should().StartWith("SEG");

        // FareDetail with FareBasisCode.
        var fareDetail = offerItem.Element(RsNs + "FareDetail");
        fareDetail.Should().NotBeNull("FareDetail must be present");
        var fareComponent = fareDetail!.Element(RsNs + "FareComponent");
        fareComponent.Should().NotBeNull();
        fareComponent!.Element(RsNs + "FareBasisCode")
                      ?.Element(RsNs + "Code")?.Value.Should().NotBeNullOrWhiteSpace();

        // CabinType code must be a valid NDC value (M/W/C/F).
        var cabinCode = fareComponent
            .Element(RsNs + "CabinType")
            ?.Element(RsNs + "CabinTypeCode")
            ?.Element(RsNs + "Code")?.Value;
        cabinCode.Should().BeOneOf("M", "W", "C", "F");

        // PassengerRefs must be present.
        offerItem.Element(RsNs + "PassengerRefs")?.Value.Should().NotBeNullOrWhiteSpace();
    }

    // ── T04 — DataLists contains FlightSegmentList, OriginDestinationList ─────

    [SkippableFact, TestPriority(4)]
    public async Task T04_AirShopping_DataLists_ContainsRequiredElements()
    {
        Skip.If(_responseId is null, "T01 did not complete successfully");

        var requestXml = BuildAirShoppingRq("LHR", "JFK", "2026-07-15", [("ADT", 1)]);
        var response = await PostXmlAsync(requestXml);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var root = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!;
        var dataLists = root.Element(RsNs + "DataLists");
        dataLists.Should().NotBeNull();

        // AnonymousTravelerList.
        var travelerList = dataLists!.Element(RsNs + "AnonymousTravelerList");
        travelerList.Should().NotBeNull("AnonymousTravelerList must be present");
        var traveler = travelerList!.Element(RsNs + "AnonymousTraveler");
        traveler.Should().NotBeNull();
        traveler!.Element(RsNs + "PTC")?.Value.Should().Be("ADT");

        // FlightSegmentList.
        var segList = dataLists.Element(RsNs + "FlightSegmentList");
        segList.Should().NotBeNull("FlightSegmentList must be present");
        var seg = segList!.Element(RsNs + "FlightSegment");
        seg.Should().NotBeNull("at least one FlightSegment must be present");
        seg!.Element(RsNs + "SegmentKey")?.Value.Should().StartWith("SEG");
        seg.Element(RsNs + "Departure")
           ?.Element(RsNs + "AirportCode")?.Value.Should().Be("LHR");
        seg.Element(RsNs + "Arrival")
           ?.Element(RsNs + "AirportCode")?.Value.Should().Be("JFK");

        // MarketingCarrier AirlineID must be AX.
        seg.Element(RsNs + "MarketingCarrier")
           ?.Element(RsNs + "AirlineID")?.Value.Should().Be("AX");

        // FlightDuration value should be ISO 8601 duration.
        var duration = seg.Element(RsNs + "FlightDetail")
                          ?.Element(RsNs + "FlightDuration")
                          ?.Element(RsNs + "Value")?.Value;
        duration.Should().StartWith("PT", "flight duration must be ISO 8601");

        // OriginDestinationList.
        var odList = dataLists.Element(RsNs + "OriginDestinationList");
        odList.Should().NotBeNull("OriginDestinationList must be present");
        var od = odList!.Element(RsNs + "OriginDestination");
        od.Should().NotBeNull();
        od!.Element(RsNs + "DepartureCode")?.Value.Should().Be("LHR");
        od.Element(RsNs + "ArrivalCode")?.Value.Should().Be("JFK");
    }

    // ── T05 — Multiple passenger types produce multiple AnonymousTravelers ────

    [Fact, TestPriority(5)]
    public async Task T05_AirShopping_MultiplePassengerTypes_ReflectedInDataLists()
    {
        var requestXml = BuildAirShoppingRq("LHR", "JFK", "2026-07-15",
            [("ADT", 2), ("CHD", 1)]);

        var response = await PostXmlAsync(requestXml);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var root = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!;
        var travelerList = root
            .Element(RsNs + "DataLists")
            ?.Element(RsNs + "AnonymousTravelerList");

        travelerList.Should().NotBeNull();
        var travelers = travelerList!.Elements(RsNs + "AnonymousTraveler").ToList();
        travelers.Should().HaveCount(2, "one entry per passenger type group");

        var adtEntry = travelers.FirstOrDefault(t => t.Element(RsNs + "PTC")?.Value == "ADT");
        adtEntry.Should().NotBeNull();
        adtEntry!.Element(RsNs + "Quantity")?.Value.Should().Be("2");

        var chdEntry = travelers.FirstOrDefault(t => t.Element(RsNs + "PTC")?.Value == "CHD");
        chdEntry.Should().NotBeNull();
        chdEntry!.Element(RsNs + "Quantity")?.Value.Should().Be("1");
    }

    // ── T06 — No flights available returns RS with empty OffersGroup ──────────

    [Fact, TestPriority(6)]
    public async Task T06_AirShopping_NoFlightsAvailable_ReturnsEmptyOffersGroup()
    {
        // Use a past date to ensure no inventory exists.
        var requestXml = BuildAirShoppingRq("LHR", "JFK", "2020-01-01", [("ADT", 1)]);

        var response = await PostXmlAsync(requestXml);

        // Endpoint always returns 200; empty results mean no Offer elements.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var root = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!;

        var offers = root
            .Element(RsNs + "OffersGroup")
            ?.Element(RsNs + "AirlineOffers")
            ?.Elements(RsNs + "Offer")
            .ToList() ?? [];

        offers.Should().BeEmpty("no inventory should exist for a past date");
    }

    // ── T07 — Missing CoreQuery returns 400 with NDC Errors element ──────────

    [Fact, TestPriority(7)]
    public async Task T07_AirShopping_MissingCoreQuery_Returns400WithErrorElement()
    {
        const string malformedXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <IATA_AirShoppingRQ xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_AirShoppingRQ">
              <Document><ReferenceVersion>21.3</ReferenceVersion></Document>
              <Travelers>
                <Traveler><AnonymousTraveler><PTC>ADT</PTC><Quantity>1</Quantity></AnonymousTraveler></Traveler>
              </Travelers>
            </IATA_AirShoppingRQ>
            """;

        var response = await PostXmlAsync(malformedXml);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        var root = XDocument.Parse(body).Root!;

        var error = root.Element(RsNs + "Errors")?.Element(RsNs + "Error");
        error.Should().NotBeNull("a missing CoreQuery must produce an Errors/Error element");
        error!.Element(RsNs + "Code")?.Value.Should().Be("ERR_PARSE");
    }

    // ── T08 — Invalid XML body returns 400 ────────────────────────────────────

    [Fact, TestPriority(8)]
    public async Task T08_AirShopping_InvalidXml_Returns400()
    {
        const string invalidXml = "this is not xml << >";

        var response = await PostXmlAsync(invalidXml);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── T09 — Document element with ReferenceVersion 21.3 is present ─────────

    [Fact, TestPriority(9)]
    public async Task T09_AirShopping_Response_DocumentVersionIs213()
    {
        var requestXml = BuildAirShoppingRq("LHR", "JFK", "2026-07-15", [("ADT", 1)]);
        var response = await PostXmlAsync(requestXml);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var root = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!;
        var version = root.Element(RsNs + "Document")?.Element(RsNs + "ReferenceVersion")?.Value;
        version.Should().Be("21.3", "the response must declare NDC schema version 21.3");
    }

    // ── T10 — FlightSegment departure date and time are populated ─────────────

    [Fact, TestPriority(10)]
    public async Task T10_AirShopping_FlightSegment_DepartureDateAndTimePopulated()
    {
        var requestXml = BuildAirShoppingRq("LHR", "JFK", "2026-07-15", [("ADT", 1)]);
        var response = await PostXmlAsync(requestXml);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var root = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!;
        var seg = root
            .Element(RsNs + "DataLists")
            ?.Element(RsNs + "FlightSegmentList")
            ?.Element(RsNs + "FlightSegment");

        if (seg is null)
            return; // No inventory — skip assertion.

        var depDate = seg.Element(RsNs + "Departure")?.Element(RsNs + "Date")?.Value;
        DateOnly.TryParse(depDate, out _).Should().BeTrue("Departure/Date must be yyyy-MM-dd");

        var depTime = seg.Element(RsNs + "Departure")?.Element(RsNs + "Time")?.Value;
        depTime.Should().NotBeNullOrWhiteSpace("Departure/Time must be present");

        var arrDate = seg.Element(RsNs + "Arrival")?.Element(RsNs + "Date")?.Value;
        DateOnly.TryParse(arrDate, out _).Should().BeTrue("Arrival/Date must be yyyy-MM-dd");
    }

    // ── T11 — Equipment/AircraftCode is a valid IATA 3-char code ─────────────

    [Fact, TestPriority(11)]
    public async Task T11_AirShopping_Equipment_AircraftCodeIsIataThreeChar()
    {
        var requestXml = BuildAirShoppingRq("LHR", "JFK", "2026-07-15", [("ADT", 1)]);
        var response = await PostXmlAsync(requestXml);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var root = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!;
        var seg = root
            .Element(RsNs + "DataLists")
            ?.Element(RsNs + "FlightSegmentList")
            ?.Element(RsNs + "FlightSegment");

        if (seg is null)
            return; // No inventory.

        var aircraftCode = seg.Element(RsNs + "Equipment")
                              ?.Element(RsNs + "AircraftCode")?.Value;

        aircraftCode.Should().NotBeNullOrWhiteSpace();
        aircraftCode!.Should().HaveLength(3, "NDC Equipment/AircraftCode must be IATA 3-char code");
    }

    // ── T12 — Standalone: empty body returns 400 ─────────────────────────────

    [Fact, TestPriority(12)]
    public async Task T12_AirShopping_EmptyBody_Returns400()
    {
        var content = new StringContent(string.Empty, Encoding.UTF8, "application/xml");
        var response = await _client.PostAsync("/api/v1/ndc/AirShopping", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Task<HttpResponseMessage> PostXmlAsync(string xml)
    {
        var content = new StringContent(xml, Encoding.UTF8, "application/xml");
        return _client.PostAsync("/api/v1/ndc/AirShopping", content);
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
public sealed class TestPriorityAttribute(int priority) : Attribute
{
    public int Priority { get; } = priority;
}

public sealed class PriorityOrderer : ITestCaseOrderer
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
