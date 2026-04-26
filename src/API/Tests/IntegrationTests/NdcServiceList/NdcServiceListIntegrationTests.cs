using System.Net;
using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace ReservationSystem.Tests.IntegrationTests.NdcServiceList;

/// <summary>
/// Integration tests for the NDC 21.3 ServiceList endpoint (POST /v1/ndc/ServiceList).
///
/// Tests run in priority order. T01 captures a live OfferId from AirShopping.
/// T02–T08 cover the happy path and structural assertions.
/// T09–T10 are error-path tests that do not depend on earlier state.
///
/// Prerequisites:
///   RETAIL_API_BASE_URL  — base URL of the Retail API (defaults to the deployed Azure URL)
///   RETAIL_API_HOST_KEY  — optional Azure Functions host key
/// </summary>
[TestCaseOrderer(
    "ReservationSystem.Tests.IntegrationTests.NdcServiceList.NdcServiceListPriorityOrderer",
    "ReservationSystem.Tests")]
public class NdcServiceListIntegrationTests : IAsyncLifetime
{
    private static readonly string BaseUrl =
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RETAIL_API_BASE_URL"))
            ? "https://reservation-system-db-api-retail-aqasakbxcje0a6eh.uksouth-01.azurewebsites.net"
            : Environment.GetEnvironmentVariable("RETAIL_API_BASE_URL")!;

    private static readonly string? HostKey =
        Environment.GetEnvironmentVariable("RETAIL_API_HOST_KEY");

    private static readonly XNamespace RsNs =
        "http://www.iata.org/IATA/2015/00/2021.3/IATA_ServiceListRS";

    private static readonly XNamespace AirShoppingRsNs =
        "http://www.iata.org/IATA/2015/00/2021.3/IATA_AirShoppingRS";

    // Shared state threaded through the ordered tests.
    private static string? _offerId;

    private readonly HttpClient _client;

    public NdcServiceListIntegrationTests()
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
    public async Task T01_ServiceList_Setup_ObtainOfferIdFromAirShopping()
    {
        var airShoppingXml = BuildAirShoppingRq("LHR", "JFK", "2026-07-15", [("ADT", 1)]);
        var content = new StringContent(airShoppingXml, Encoding.UTF8, "application/xml");
        var response = await _client.PostAsync("/api/v1/ndc/AirShopping", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var root = XDocument.Parse(body).Root!;

        _offerId = root
            .Element(AirShoppingRsNs + "OffersGroup")
            ?.Element(AirShoppingRsNs + "AirlineOffers")
            ?.Element(AirShoppingRsNs + "Offer")
            ?.Element(AirShoppingRsNs + "OfferID")?.Value;

        Skip.If(string.IsNullOrWhiteSpace(_offerId),
            "No offers returned by AirShopping — no inventory for LHR→JFK on 2026-07-15.");
    }

    // ── T02 — ServiceListRQ without OfferRefID returns 200 with services ───────

    [Fact, TestPriority(2)]
    public async Task T02_ServiceList_WithoutOfferRefId_Returns200WithServiceListRS()
    {
        var requestXml = BuildServiceListRq(offerRefId: null, cabinCode: null);

        var response = await PostServiceListXmlAsync(requestXml);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "ServiceList without OfferRefID must return 200 OK with the full SSR catalogue");
        response.Content.Headers.ContentType?.MediaType.Should().StartWith("application/xml");

        var body = await response.Content.ReadAsStringAsync();
        var root = XDocument.Parse(body).Root;

        root.Should().NotBeNull();
        root!.Name.LocalName.Should().Be("IATA_ServiceListRS");

        var responseEl = root.Element(RsNs + "Response");
        responseEl.Should().NotBeNull("Response element must be present");

        var serviceList = responseEl!.Element(RsNs + "ServiceList");
        serviceList.Should().NotBeNull("Response/ServiceList must be present");
    }

    // ── T03 — ServiceListRQ with OfferRefID returns 200 with context-filtered services ─

    [SkippableFact, TestPriority(3)]
    public async Task T03_ServiceList_WithOfferRefId_Returns200WithFilteredServices()
    {
        Skip.If(_offerId is null, "T01 did not produce an OfferId.");

        var requestXml = BuildServiceListRq(offerRefId: _offerId, cabinCode: null);

        var response = await PostServiceListXmlAsync(requestXml);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "ServiceList with a valid OfferRefID must return 200 OK");

        var root = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!;
        root.Name.LocalName.Should().Be("IATA_ServiceListRS");
        root.Element(RsNs + "Response")?.Element(RsNs + "ServiceList")
            .Should().NotBeNull("Response/ServiceList must be present");
    }

    // ── T04 — Document declares NDC version 21.3 ─────────────────────────────

    [Fact, TestPriority(4)]
    public async Task T04_ServiceList_Response_DocumentVersionIs213()
    {
        var requestXml = BuildServiceListRq(offerRefId: null, cabinCode: null);
        var response = await PostServiceListXmlAsync(requestXml);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var root = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!;
        var version = root.Element(RsNs + "Document")?.Element(RsNs + "ReferenceVersion")?.Value;
        version.Should().Be("21.3", "the response must declare NDC schema version 21.3");
    }

    // ── T05 — ALaCarteOffer has Owner=AX ─────────────────────────────────────

    [Fact, TestPriority(5)]
    public async Task T05_ServiceList_ALaCarteOffer_HasApexAirOwner()
    {
        var requestXml = BuildServiceListRq(offerRefId: null, cabinCode: null);
        var response = await PostServiceListXmlAsync(requestXml);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var root = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!;
        var offer = root
            .Element(RsNs + "Response")
            ?.Element(RsNs + "ServiceList")
            ?.Element(RsNs + "ALaCarteOffer");

        offer.Should().NotBeNull("ALaCarteOffer must be present when services exist");
        offer!.Element(RsNs + "Owner")?.Value.Should().Be("AX",
            "the owning airline must be AX (Apex Air)");
    }

    // ── T06 — ALaCarteOfferItem has required NDC fields ──────────────────────

    [Fact, TestPriority(6)]
    public async Task T06_ServiceList_ALaCarteOfferItem_ContainsRequiredFields()
    {
        var requestXml = BuildServiceListRq(offerRefId: null, cabinCode: null);
        var response = await PostServiceListXmlAsync(requestXml);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var root = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!;
        var offer = root
            .Element(RsNs + "Response")
            ?.Element(RsNs + "ServiceList")
            ?.Element(RsNs + "ALaCarteOffer");

        var item = offer?.Element(RsNs + "ALaCarteOfferItem");
        item.Should().NotBeNull("at least one ALaCarteOfferItem must be present");

        // OfferItemID must follow the SLI- prefix convention.
        item!.Element(RsNs + "OfferItemID")?.Value.Should().StartWith("SLI-",
            "OfferItemID must be prefixed SLI-");

        // Eligibility must be present.
        item.Element(RsNs + "Eligibility").Should().NotBeNull("Eligibility must be present");

        // Service/ServiceCode/Code must be present.
        var serviceCode = item
            .Element(RsNs + "Service")
            ?.Element(RsNs + "ServiceCode")
            ?.Element(RsNs + "Code");
        serviceCode.Should().NotBeNull("Service/ServiceCode/Code must be present");

        // UnitPriceDetail/TotalAmount must be 0.00 (indicative price).
        var unitPrice = item
            .Element(RsNs + "UnitPriceDetail")
            ?.Element(RsNs + "TotalAmount")
            ?.Element(RsNs + "SimpleCurrencyPrice");
        unitPrice.Should().NotBeNull("UnitPriceDetail/TotalAmount/SimpleCurrencyPrice must be present");
        unitPrice!.Attribute("CurCode")?.Value.Should().NotBeNullOrWhiteSpace("CurCode must be set");
        decimal.TryParse(unitPrice.Value, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var price).Should().BeTrue();
        price.Should().Be(0m, "indicative ancillary prices are 0.00 in ServiceListRS");
    }

    // ── T07 — DataLists/ServiceDefinitionList is populated ───────────────────

    [Fact, TestPriority(7)]
    public async Task T07_ServiceList_DataLists_ContainsServiceDefinitionList()
    {
        var requestXml = BuildServiceListRq(offerRefId: null, cabinCode: null);
        var response = await PostServiceListXmlAsync(requestXml);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var root = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!;
        var defList = root
            .Element(RsNs + "Response")
            ?.Element(RsNs + "DataLists")
            ?.Element(RsNs + "ServiceDefinitionList");

        defList.Should().NotBeNull("DataLists/ServiceDefinitionList must be present");

        var def = defList!.Element(RsNs + "ServiceDefinition");
        def.Should().NotBeNull("at least one ServiceDefinition must be present");

        def!.Element(RsNs + "ServiceDefinitionID")?.Value.Should().StartWith("SD-",
            "ServiceDefinitionID must be prefixed SD-");
        def.Element(RsNs + "Name")?.Value.Should().NotBeNullOrWhiteSpace("Name must be set");
        def.Element(RsNs + "ServiceCode")?.Element(RsNs + "Code")
            ?.Value.Should().NotBeNullOrWhiteSpace("ServiceCode/Code must be set");
    }

    // ── T08 — ServiceList with explicit NDC cabin code filters by cabin ───────

    [Fact, TestPriority(8)]
    public async Task T08_ServiceList_WithCabinCode_Returns200()
    {
        // Economy cabin (NDC code M = internal Y).
        var requestXml = BuildServiceListRq(offerRefId: null, cabinCode: "M");
        var response = await PostServiceListXmlAsync(requestXml);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "ServiceList with CabinTypeCode must still return 200 OK");

        var root = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!;
        root.Name.LocalName.Should().Be("IATA_ServiceListRS");
        root.Element(RsNs + "Response")?.Element(RsNs + "ServiceList")
            .Should().NotBeNull("Response/ServiceList must be present");
    }

    // ── T09 — Invalid XML body returns 400 with IATA_ServiceListRS error ──────

    [Fact, TestPriority(9)]
    public async Task T09_ServiceList_InvalidXml_Returns400WithErrorEnvelope()
    {
        var response = await PostServiceListXmlAsync("not xml << > garbage");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().StartWith("application/xml");

        var body = await response.Content.ReadAsStringAsync();
        var root = XDocument.Parse(body).Root!;

        root.Name.LocalName.Should().Be("IATA_ServiceListRS",
            "error responses must use the IATA_ServiceListRS envelope");

        var error = root.Element(RsNs + "Errors")?.Element(RsNs + "Error");
        error.Should().NotBeNull("an Errors/Error element must be present");
        error!.Element(RsNs + "Code")?.Value.Should().Be("ERR_PARSE");
        error.Element(RsNs + "LangCode")?.Value.Should().Be("EN");
    }

    // ── T10 — Empty body returns 400 ─────────────────────────────────────────

    [Fact, TestPriority(10)]
    public async Task T10_ServiceList_EmptyBody_Returns400()
    {
        var content = new StringContent(string.Empty, Encoding.UTF8, "application/xml");
        var response = await _client.PostAsync("/api/v1/ndc/ServiceList", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var root = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!;
        root.Name.LocalName.Should().Be("IATA_ServiceListRS");
        root.Element(RsNs + "Errors")?.Element(RsNs + "Error")
            .Should().NotBeNull("an Errors/Error element must be present for an empty body");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Task<HttpResponseMessage> PostServiceListXmlAsync(string xml)
    {
        var content = new StringContent(xml, Encoding.UTF8, "application/xml");
        return _client.PostAsync("/api/v1/ndc/ServiceList", content);
    }

    private static string BuildServiceListRq(string? offerRefId, string? cabinCode)
    {
        var offerRefXml = offerRefId is not null
            ? $"""
                  <SelectionCriteria>
                    <OfferRef>
                      <OfferRefID>{offerRefId}</OfferRefID>
                    </OfferRef>
                  </SelectionCriteria>
              """
            : string.Empty;

        var cabinXml = cabinCode is not null
            ? $"""
                  <CabinPreferences>
                    <CabinType>
                      <CabinTypeCode>
                        <Code>{cabinCode}</Code>
                      </CabinTypeCode>
                    </CabinType>
                  </CabinPreferences>
              """
            : string.Empty;

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <IATA_ServiceListRQ xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_ServiceListRQ">
              <Document>
                <Name>Apex Air NDC ServiceList Test</Name>
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
                <Traveler>
                  <AnonymousTraveler>
                    <PTC>ADT</PTC>
                    <Quantity>1</Quantity>
                  </AnonymousTraveler>
                </Traveler>
              </Travelers>
              <Query>
                {offerRefXml}
                {cabinXml}
              </Query>
            </IATA_ServiceListRQ>
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
file sealed class TestPriorityAttribute(int priority) : Attribute
{
    public int Priority { get; } = priority;
}

public sealed class NdcServiceListPriorityOrderer : ITestCaseOrderer
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
