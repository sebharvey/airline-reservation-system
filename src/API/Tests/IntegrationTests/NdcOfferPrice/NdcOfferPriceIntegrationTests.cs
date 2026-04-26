using System.Net;
using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace ReservationSystem.Tests.IntegrationTests.NdcOfferPrice;

/// <summary>
/// Integration tests for the NDC 21.3 OfferPrice endpoint (POST /v1/ndc/OfferPrice).
///
/// Tests run against the live Retail API and require a valid offer obtained via a
/// preceding AirShopping call. Set RETAIL_API_BASE_URL and (optionally)
/// RETAIL_API_HOST_KEY before running.
///
/// Test ordering:
///   T01–T02  Bootstrap — obtain an offer ID from AirShopping.
///   T03–T09  Happy path — validate the OfferPriceRS structure and content.
///   T10–T13  Error cases — missing/invalid inputs return correct error responses.
/// </summary>
[TestCaseOrderer(
    "ReservationSystem.Tests.IntegrationTests.NdcOfferPrice.OpPriorityOrderer",
    "ReservationSystem.Tests")]
public class NdcOfferPriceIntegrationTests : IAsyncLifetime
{
    private static readonly string BaseUrl =
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RETAIL_API_BASE_URL"))
            ? "https://reservation-system-db-api-retail-aqasakbxcje0a6eh.uksouth-01.azurewebsites.net"
            : Environment.GetEnvironmentVariable("RETAIL_API_BASE_URL")!;

    private static readonly string? HostKey =
        Environment.GetEnvironmentVariable("RETAIL_API_HOST_KEY");

    private static readonly XNamespace ShoppingRsNs =
        "http://www.iata.org/IATA/2015/00/2021.3/IATA_AirShoppingRS";

    private static readonly XNamespace PriceRsNs =
        "http://www.iata.org/IATA/2015/00/2021.3/IATA_OfferPriceRS";

    // State shared across ordered tests.
    private static string? _offerIdFromShopping;
    private static string? _offerItemIdFromShopping;

    private readonly HttpClient _client;

    public NdcOfferPriceIntegrationTests()
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

    // ── T01 — AirShopping bootstrap: capture an offer ID ─────────────────────

    [Fact, OpPriority(1)]
    public async Task T01_Bootstrap_AirShopping_CapturesOfferId()
    {
        var xml = BuildAirShoppingRq("LHR", "JFK", "2026-07-15", [("ADT", 1)]);
        var response = await PostXmlAsync("/api/v1/ndc/AirShopping", xml);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var root = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!;

        var firstOffer = root
            .Element(ShoppingRsNs + "OffersGroup")
            ?.Element(ShoppingRsNs + "AirlineOffers")
            ?.Elements(ShoppingRsNs + "Offer")
            .FirstOrDefault();

        Skip.If(firstOffer is null, "No offers returned by AirShopping — no inventory available");

        var offerIdEl = firstOffer!.Element(ShoppingRsNs + "OfferID");
        offerIdEl.Should().NotBeNull();
        Guid.TryParse(offerIdEl!.Value, out _).Should().BeTrue("OfferID must be a GUID");
        _offerIdFromShopping = offerIdEl.Value;

        var offerItemIdEl = firstOffer
            .Element(ShoppingRsNs + "OfferItem")
            ?.Element(ShoppingRsNs + "OfferItemID");
        _offerItemIdFromShopping = offerItemIdEl?.Value;
    }

    // ── T02 — Valid OfferPriceRQ returns 200 with OfferPriceRS ───────────────

    [SkippableFact, OpPriority(2)]
    public async Task T02_OfferPrice_ValidRequest_Returns200WithOfferPriceRS()
    {
        Skip.If(_offerIdFromShopping is null, "T01 did not capture an offer ID");

        var xml = BuildOfferPriceRq(_offerIdFromShopping!, _offerItemIdFromShopping);
        var response = await PostXmlAsync("/api/v1/ndc/OfferPrice", xml);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().StartWith("application/xml");

        var body = await response.Content.ReadAsStringAsync();
        var root = XDocument.Parse(body).Root;

        root.Should().NotBeNull();
        root!.Name.LocalName.Should().Be("IATA_OfferPriceRS");
        root.Name.NamespaceName.Should().Be("http://www.iata.org/IATA/2015/00/2021.3/IATA_OfferPriceRS");
    }

    // ── T03 — Response document declares ReferenceVersion 21.3 ───────────────

    [SkippableFact, OpPriority(3)]
    public async Task T03_OfferPrice_Response_DocumentVersionIs213()
    {
        Skip.If(_offerIdFromShopping is null, "T01 did not capture an offer ID");

        var xml = BuildOfferPriceRq(_offerIdFromShopping!, _offerItemIdFromShopping);
        var root = await PostAndParseRootAsync("/api/v1/ndc/OfferPrice", xml);

        var version = root
            .Element(PriceRsNs + "Document")
            ?.Element(PriceRsNs + "ReferenceVersion")?.Value;

        version.Should().Be("21.3", "the OfferPriceRS must declare NDC schema version 21.3");
    }

    // ── T04 — PricedOffer has required top-level elements ─────────────────────

    [SkippableFact, OpPriority(4)]
    public async Task T04_OfferPrice_PricedOffer_HasRequiredTopLevelElements()
    {
        Skip.If(_offerIdFromShopping is null, "T01 did not capture an offer ID");

        var xml = BuildOfferPriceRq(_offerIdFromShopping!, _offerItemIdFromShopping);
        var root = await PostAndParseRootAsync("/api/v1/ndc/OfferPrice", xml);

        var pricedOffer = root.Element(PriceRsNs + "PricedOffer");
        pricedOffer.Should().NotBeNull("PricedOffer must be present in OfferPriceRS");

        // OfferID must match the request OfferRefID.
        var offerIdEl = pricedOffer!.Element(PriceRsNs + "OfferID");
        offerIdEl.Should().NotBeNull("PricedOffer/OfferID must be present");
        offerIdEl!.Value.Should().Be(_offerIdFromShopping, "PricedOffer/OfferID must match the requested OfferRefID");

        // Carrier codes.
        pricedOffer.Element(PriceRsNs + "OwnerCode")?.Value.Should().Be("AX");
        pricedOffer.Element(PriceRsNs + "ValidatingCarrierCode")?.Value.Should().Be("AX");

        // TotalPrice.
        var totalAmount = pricedOffer
            .Element(PriceRsNs + "TotalPrice")
            ?.Element(PriceRsNs + "TotalAmount");
        totalAmount.Should().NotBeNull("TotalPrice/TotalAmount must be present");
        totalAmount!.Attribute("CurCode")?.Value.Should().NotBeNullOrWhiteSpace("CurCode attribute is required");
        decimal.TryParse(totalAmount.Value, out var total).Should().BeTrue();
        total.Should().BeGreaterThan(0);
    }

    // ── T05 — OfferItem has TotalPriceDetail, Service, and FareDetail ─────────

    [SkippableFact, OpPriority(5)]
    public async Task T05_OfferPrice_OfferItem_HasRequiredStructure()
    {
        Skip.If(_offerIdFromShopping is null, "T01 did not capture an offer ID");

        var xml = BuildOfferPriceRq(_offerIdFromShopping!, _offerItemIdFromShopping);
        var root = await PostAndParseRootAsync("/api/v1/ndc/OfferPrice", xml);

        var offerItem = root
            .Element(PriceRsNs + "PricedOffer")
            ?.Elements(PriceRsNs + "OfferItem")
            .FirstOrDefault();

        offerItem.Should().NotBeNull("PricedOffer must contain at least one OfferItem");

        // OfferItemID.
        offerItem!.Element(PriceRsNs + "OfferItemID")?.Value.Should().StartWith("ITEM-");

        // TotalPriceDetail with base amount and tax total.
        var tpd = offerItem.Element(PriceRsNs + "TotalPriceDetail");
        tpd.Should().NotBeNull("TotalPriceDetail must be present");
        tpd!.Element(PriceRsNs + "TotalAmount")
            ?.Element(PriceRsNs + "SimpleCurrencyPrice").Should().NotBeNull("SimpleCurrencyPrice is required");
        tpd.Element(PriceRsNs + "BaseAmount").Should().NotBeNull("BaseAmount is required");

        var taxesTotal = tpd
            .Element(PriceRsNs + "Taxes")
            ?.Element(PriceRsNs + "Total");
        taxesTotal.Should().NotBeNull("Taxes/Total is required");
        taxesTotal!.Attribute("CurCode")?.Value.Should().NotBeNullOrWhiteSpace();

        // Service with FlightRefs.
        var service = offerItem.Element(PriceRsNs + "Service");
        service.Should().NotBeNull("Service must be present");
        service!.Element(PriceRsNs + "FlightRefs")?.Value.Should().StartWith("SEG");

        // FareDetail with cabin code.
        var fareComponent = offerItem
            .Element(PriceRsNs + "FareDetail")
            ?.Element(PriceRsNs + "FareComponent");
        fareComponent.Should().NotBeNull("FareDetail/FareComponent must be present");

        var fareBasisCode = fareComponent!
            .Element(PriceRsNs + "FareBasisCode")
            ?.Element(PriceRsNs + "Code")?.Value;
        fareBasisCode.Should().NotBeNullOrWhiteSpace("FareBasisCode/Code must be populated");

        var cabinCode = fareComponent
            .Element(PriceRsNs + "CabinType")
            ?.Element(PriceRsNs + "CabinTypeCode")
            ?.Element(PriceRsNs + "Code")?.Value;
        cabinCode.Should().BeOneOf("M", "W", "C", "F", "NDC cabin code must be M/W/C/F");
    }

    // ── T06 — OfferExpiration is present with a parseable UTC DateTime ─────────

    [SkippableFact, OpPriority(6)]
    public async Task T06_OfferPrice_PricedOffer_OfferExpirationIsPresent()
    {
        Skip.If(_offerIdFromShopping is null, "T01 did not capture an offer ID");

        var xml = BuildOfferPriceRq(_offerIdFromShopping!, _offerItemIdFromShopping);
        var root = await PostAndParseRootAsync("/api/v1/ndc/OfferPrice", xml);

        var expiryStr = root
            .Element(PriceRsNs + "PricedOffer")
            ?.Element(PriceRsNs + "OfferExpiration")
            ?.Element(PriceRsNs + "DateTime")?.Value;

        expiryStr.Should().NotBeNullOrWhiteSpace("OfferExpiration/DateTime must be present");
        DateTimeOffset.TryParse(expiryStr, out var expiry).Should()
            .BeTrue("OfferExpiration/DateTime must be a valid ISO 8601 timestamp");
        expiry.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-5),
            "offer expiry should be in the future (or very recently past in slow test environments)");
    }

    // ── T07 — DataLists contains FlightSegmentList and OriginDestinationList ──

    [SkippableFact, OpPriority(7)]
    public async Task T07_OfferPrice_DataLists_ContainsFlightSegmentAndOriginDestination()
    {
        Skip.If(_offerIdFromShopping is null, "T01 did not capture an offer ID");

        var xml = BuildOfferPriceRq(_offerIdFromShopping!, _offerItemIdFromShopping);
        var root = await PostAndParseRootAsync("/api/v1/ndc/OfferPrice", xml);

        var dataLists = root.Element(PriceRsNs + "DataLists");
        dataLists.Should().NotBeNull("DataLists must be present");

        // FlightSegmentList.
        var seg = dataLists!
            .Element(PriceRsNs + "FlightSegmentList")
            ?.Element(PriceRsNs + "FlightSegment");
        seg.Should().NotBeNull("FlightSegmentList must contain a FlightSegment");
        seg!.Element(PriceRsNs + "SegmentKey")?.Value.Should().StartWith("SEG");

        // Departure and arrival airports.
        seg.Element(PriceRsNs + "Departure")
           ?.Element(PriceRsNs + "AirportCode")?.Value.Should().HaveLength(3);
        seg.Element(PriceRsNs + "Arrival")
           ?.Element(PriceRsNs + "AirportCode")?.Value.Should().HaveLength(3);

        // MarketingCarrier must be AX.
        seg.Element(PriceRsNs + "MarketingCarrier")
           ?.Element(PriceRsNs + "AirlineID")?.Value.Should().Be("AX");

        // Equipment AircraftCode must be 3 chars.
        var aircraft = seg.Element(PriceRsNs + "Equipment")
                          ?.Element(PriceRsNs + "AircraftCode")?.Value;
        aircraft.Should().HaveLength(3, "NDC Equipment/AircraftCode must be IATA 3-char code");

        // OriginDestinationList.
        var od = dataLists
            .Element(PriceRsNs + "OriginDestinationList")
            ?.Element(PriceRsNs + "OriginDestination");
        od.Should().NotBeNull("OriginDestinationList must contain an OriginDestination");
        od!.Element(PriceRsNs + "DepartureCode")?.Value.Should().HaveLength(3);
        od.Element(PriceRsNs + "ArrivalCode")?.Value.Should().HaveLength(3);
    }

    // ── T08 — Travelers in request produce AnonymousTravelerList in response ──

    [SkippableFact, OpPriority(8)]
    public async Task T08_OfferPrice_WithTravelers_AnonymousTravelerListPresent()
    {
        Skip.If(_offerIdFromShopping is null, "T01 did not capture an offer ID");

        var xml = BuildOfferPriceRq(_offerIdFromShopping!, _offerItemIdFromShopping,
            passengers: [("ADT", 2), ("CHD", 1)]);
        var root = await PostAndParseRootAsync("/api/v1/ndc/OfferPrice", xml);

        var travelerList = root
            .Element(PriceRsNs + "DataLists")
            ?.Element(PriceRsNs + "AnonymousTravelerList");

        travelerList.Should().NotBeNull("AnonymousTravelerList must be present when Travelers are in the request");

        var travelers = travelerList!.Elements(PriceRsNs + "AnonymousTraveler").ToList();
        travelers.Should().HaveCount(2, "one entry per passenger type group");

        var adt = travelers.FirstOrDefault(t => t.Element(PriceRsNs + "PTC")?.Value == "ADT");
        adt.Should().NotBeNull();
        adt!.Element(PriceRsNs + "Quantity")?.Value.Should().Be("2");

        var chd = travelers.FirstOrDefault(t => t.Element(PriceRsNs + "PTC")?.Value == "CHD");
        chd.Should().NotBeNull();
        chd!.Element(PriceRsNs + "Quantity")?.Value.Should().Be("1");
    }

    // ── T09 — Departure and arrival dates in FlightSegment are valid dates ────

    [SkippableFact, OpPriority(9)]
    public async Task T09_OfferPrice_FlightSegment_DatesAreValid()
    {
        Skip.If(_offerIdFromShopping is null, "T01 did not capture an offer ID");

        var xml = BuildOfferPriceRq(_offerIdFromShopping!, _offerItemIdFromShopping);
        var root = await PostAndParseRootAsync("/api/v1/ndc/OfferPrice", xml);

        var seg = root
            .Element(PriceRsNs + "DataLists")
            ?.Element(PriceRsNs + "FlightSegmentList")
            ?.Element(PriceRsNs + "FlightSegment");

        if (seg is null) return;

        var depDate = seg.Element(PriceRsNs + "Departure")?.Element(PriceRsNs + "Date")?.Value;
        DateOnly.TryParse(depDate, out _).Should().BeTrue("Departure/Date must be yyyy-MM-dd");

        var depTime = seg.Element(PriceRsNs + "Departure")?.Element(PriceRsNs + "Time")?.Value;
        depTime.Should().NotBeNullOrWhiteSpace("Departure/Time must be present");

        var arrDate = seg.Element(PriceRsNs + "Arrival")?.Element(PriceRsNs + "Date")?.Value;
        DateOnly.TryParse(arrDate, out _).Should().BeTrue("Arrival/Date must be yyyy-MM-dd");
    }

    // ── T10 — Unknown OfferRefID returns 404 with Errors element ─────────────

    [Fact, OpPriority(10)]
    public async Task T10_OfferPrice_UnknownOfferRefId_Returns404WithErrors()
    {
        var unknownId = Guid.NewGuid().ToString();
        var xml = BuildOfferPriceRq(unknownId, offerItemRefId: null);

        var response = await PostXmlAsync("/api/v1/ndc/OfferPrice", xml);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var root = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!;
        root.Name.LocalName.Should().Be("IATA_OfferPriceRS");

        var error = root
            .Element(PriceRsNs + "Errors")
            ?.Element(PriceRsNs + "Error");
        error.Should().NotBeNull("an unknown offer must produce an Errors/Error element");
        error!.Element(PriceRsNs + "Code")?.Value.Should().Be("ERR_OFFER_NOT_FOUND");
    }

    // ── T11 — Missing SelectedOffer returns 400 with ERR_PARSE ───────────────

    [Fact, OpPriority(11)]
    public async Task T11_OfferPrice_MissingSelectedOffer_Returns400WithErrorElement()
    {
        const string malformed = """
            <?xml version="1.0" encoding="UTF-8"?>
            <IATA_OfferPriceRQ xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_OfferPriceRQ">
              <Document><ReferenceVersion>21.3</ReferenceVersion></Document>
            </IATA_OfferPriceRQ>
            """;

        var response = await PostXmlAsync("/api/v1/ndc/OfferPrice", malformed);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var root = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!;

        root.Element(PriceRsNs + "Errors")
            ?.Element(PriceRsNs + "Error")
            ?.Element(PriceRsNs + "Code")?.Value
            .Should().Be("ERR_PARSE");
    }

    // ── T12 — Non-GUID OfferRefID returns 400 ────────────────────────────────

    [Fact, OpPriority(12)]
    public async Task T12_OfferPrice_NonGuidOfferRefId_Returns400()
    {
        const string badId = """
            <?xml version="1.0" encoding="UTF-8"?>
            <IATA_OfferPriceRQ xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_OfferPriceRQ">
              <Document><ReferenceVersion>21.3</ReferenceVersion></Document>
              <SelectedOffer>
                <OfferRefID>not-a-guid</OfferRefID>
                <OwnerCode>AX</OwnerCode>
              </SelectedOffer>
            </IATA_OfferPriceRQ>
            """;

        var response = await PostXmlAsync("/api/v1/ndc/OfferPrice", badId);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var root = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!;
        root.Element(PriceRsNs + "Errors")?.Element(PriceRsNs + "Error").Should().NotBeNull();
    }

    // ── T13 — Empty body returns 400 ─────────────────────────────────────────

    [Fact, OpPriority(13)]
    public async Task T13_OfferPrice_EmptyBody_Returns400()
    {
        var content = new StringContent(string.Empty, Encoding.UTF8, "application/xml");
        var response = await _client.PostAsync("/api/v1/ndc/OfferPrice", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Task<HttpResponseMessage> PostXmlAsync(string path, string xml)
    {
        var content = new StringContent(xml, Encoding.UTF8, "application/xml");
        return _client.PostAsync(path, content);
    }

    private async Task<XElement> PostAndParseRootAsync(string path, string xml)
    {
        var response = await PostXmlAsync(path, xml);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!;
    }

    private static string BuildOfferPriceRq(
        string offerRefId,
        string? offerItemRefId,
        IEnumerable<(string Ptc, int Qty)>? passengers = null)
    {
        var offerItemRefXml = offerItemRefId is not null
            ? $"""
                    <OfferItemRef>
                      <OfferItemRefID>{offerItemRefId}</OfferItemRefID>
                    </OfferItemRef>
                """
            : string.Empty;

        var travelersXml = string.Empty;
        if (passengers is not null)
        {
            var paxLines = string.Join(Environment.NewLine,
                passengers.Select(p => $"""
                        <Traveler>
                          <AnonymousTraveler>
                            <PTC>{p.Ptc}</PTC>
                            <Quantity>{p.Qty}</Quantity>
                          </AnonymousTraveler>
                        </Traveler>
                    """));

            travelersXml = $"""
                  <Travelers>
                    {paxLines}
                  </Travelers>
                """;
        }

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <IATA_OfferPriceRQ xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_OfferPriceRQ">
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
              {travelersXml}
              <SelectedOffer>
                <OfferRefID>{offerRefId}</OfferRefID>
                <OwnerCode>AX</OwnerCode>
                {offerItemRefXml}
              </SelectedOffer>
            </IATA_OfferPriceRQ>
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
public sealed class OpPriorityAttribute(int priority) : Attribute
{
    public int Priority { get; } = priority;
}

public sealed class OpPriorityOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
    {
        var sorted = new SortedDictionary<int, List<TTestCase>>();

        foreach (var testCase in testCases)
        {
            var priority = testCase.TestMethod.Method
                .GetCustomAttributes(typeof(OpPriorityAttribute).AssemblyQualifiedName!, false)
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
