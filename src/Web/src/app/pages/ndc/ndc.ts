import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

interface NdcEndpoint {
  name: string;
  method: string;
  description: string;
  version: string;
  implemented: boolean;
  requestXml?: string;
  responseXml?: string;
}

const NDC_ENDPOINTS: NdcEndpoint[] = [
  {
    name: 'AirShopping',
    method: 'POST',
    description: 'Search for available flights and return priced offers for a given itinerary.',
    version: '21.3',
    implemented: true,
    requestXml: `<?xml version="1.0" encoding="UTF-8"?>
<IATA_AirShoppingRQ xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_AirShoppingRQ">
  <CoreQuery>
    <OriginDestinations>
      <OriginDestination>
        <Departure>
          <AirportCode>LHR</AirportCode>
          <Date>2026-07-15</Date>
        </Departure>
        <Arrival>
          <AirportCode>JFK</AirportCode>
        </Arrival>
      </OriginDestination>
    </OriginDestinations>
  </CoreQuery>
  <Travelers>
    <Traveler>
      <AnonymousTraveler>
        <PTC>ADT</PTC>
        <Quantity>1</Quantity>
      </AnonymousTraveler>
    </Traveler>
  </Travelers>
</IATA_AirShoppingRQ>`,
    responseXml: `<?xml version="1.0" encoding="UTF-8"?>
<IATA_AirShoppingRS xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_AirShoppingRS">
  <Document>
    <ReferenceVersion>21.3</ReferenceVersion>
  </Document>
  <ShoppingResponseID>
    <ResponseID>a1b2c3d4-e5f6-7890-abcd-ef1234567890</ResponseID>
  </ShoppingResponseID>
  <OffersGroup>
    <AirlineOffers>
      <Offer>
        <OfferID>f47ac10b-58cc-4372-a567-0e02b2c3d479</OfferID>
        <OwnerCode>AX</OwnerCode>
        <ValidatingCarrierCode>AX</ValidatingCarrierCode>
        <TotalPrice>
          <TotalAmount CurCode="GBP">542.00</TotalAmount>
        </TotalPrice>
        <OfferItem>
          <OfferItemID>ITEM-f47ac10b58cc4372a5670e02b2c3d479</OfferItemID>
          <TotalPriceDetail>
            <TotalAmount>
              <SimpleCurrencyPrice CurCode="GBP">542.00</SimpleCurrencyPrice>
            </TotalAmount>
            <BaseAmount CurCode="GBP">480.00</BaseAmount>
            <Taxes>
              <Total CurCode="GBP">62.00</Total>
            </Taxes>
          </TotalPriceDetail>
          <Service>
            <ServiceID>SVC-f47ac10b58cc4372a5670e02b2c3d479</ServiceID>
            <FlightRefs>SEG1</FlightRefs>
          </Service>
          <FareDetail>
            <FareComponent>
              <FareBasisCode>
                <Code>YOWGB</Code>
              </FareBasisCode>
              <CabinType>
                <CabinTypeCode>
                  <Code>M</Code>
                </CabinTypeCode>
              </CabinType>
              <SegmentRefs>SEG1</SegmentRefs>
              <FareRules />
            </FareComponent>
          </FareDetail>
          <PassengerRefs>PAX1</PassengerRefs>
        </OfferItem>
      </Offer>
    </AirlineOffers>
  </OffersGroup>
  <DataLists>
    <AnonymousTravelerList>
      <AnonymousTraveler>
        <ObjectKey>PAX1</ObjectKey>
        <PTC>ADT</PTC>
        <Quantity>1</Quantity>
      </AnonymousTraveler>
    </AnonymousTravelerList>
    <FlightSegmentList>
      <FlightSegment>
        <SegmentKey>SEG1</SegmentKey>
        <Departure>
          <AirportCode>LHR</AirportCode>
          <Date>2026-07-15</Date>
          <Time>10:00</Time>
        </Departure>
        <Arrival>
          <AirportCode>JFK</AirportCode>
          <Date>2026-07-15</Date>
          <Time>13:10</Time>
        </Arrival>
        <MarketingCarrier>
          <AirlineID>AX</AirlineID>
          <FlightNumber>001</FlightNumber>
          <Name>Apex Air</Name>
        </MarketingCarrier>
        <OperatingCarrier>
          <AirlineID>AX</AirlineID>
          <FlightNumber>001</FlightNumber>
        </OperatingCarrier>
        <Equipment>
          <AircraftCode>351</AircraftCode>
        </Equipment>
        <FlightDetail>
          <FlightDuration>
            <Value>PT7H10M</Value>
          </FlightDuration>
        </FlightDetail>
      </FlightSegment>
    </FlightSegmentList>
    <OriginDestinationList>
      <OriginDestination>
        <OriginDestinationKey>OD1</OriginDestinationKey>
        <DepartureCode>LHR</DepartureCode>
        <ArrivalCode>JFK</ArrivalCode>
        <FlightReferences>SEG1</FlightReferences>
      </OriginDestination>
    </OriginDestinationList>
  </DataLists>
</IATA_AirShoppingRS>`
  },
  {
    name: 'OfferPrice',
    method: 'POST',
    description: 'Reprice and confirm availability of a selected offer before order creation.',
    version: '21.3',
    implemented: true,
    requestXml: `<?xml version="1.0" encoding="UTF-8"?>
<IATA_OfferPriceRQ xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_OfferPriceRQ">
  <ShoppingResponseID>
    <ResponseID>a1b2c3d4-e5f6-7890-abcd-ef1234567890</ResponseID>
  </ShoppingResponseID>
  <SelectedOffer>
    <OfferRefID>f47ac10b-58cc-4372-a567-0e02b2c3d479</OfferRefID>
    <OfferItemRef>
      <OfferItemRefID>ITEM-f47ac10b58cc4372a5670e02b2c3d479</OfferItemRefID>
    </OfferItemRef>
  </SelectedOffer>
  <Travelers>
    <Traveler>
      <AnonymousTraveler>
        <PTC>ADT</PTC>
        <Quantity>1</Quantity>
      </AnonymousTraveler>
    </Traveler>
  </Travelers>
</IATA_OfferPriceRQ>`,
    responseXml: `<?xml version="1.0" encoding="UTF-8"?>
<IATA_OfferPriceRS xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_OfferPriceRS">
  <Document>
    <ReferenceVersion>21.3</ReferenceVersion>
  </Document>
  <PricedOffer>
    <OfferID>f47ac10b-58cc-4372-a567-0e02b2c3d479</OfferID>
    <OwnerCode>AX</OwnerCode>
    <ValidatingCarrierCode>AX</ValidatingCarrierCode>
    <TotalPrice>
      <TotalAmount CurCode="GBP">542.00</TotalAmount>
    </TotalPrice>
    <OfferItem>
      <OfferItemID>ITEM-f47ac10b58cc4372a5670e02b2c3d479</OfferItemID>
      <TotalPriceDetail>
        <TotalAmount>
          <SimpleCurrencyPrice CurCode="GBP">542.00</SimpleCurrencyPrice>
        </TotalAmount>
        <BaseAmount CurCode="GBP">480.00</BaseAmount>
        <Taxes>
          <Total CurCode="GBP">62.00</Total>
          <Breakdown>
            <Tax>
              <Amount CurCode="GBP">52.00</Amount>
              <TaxCode>UB</TaxCode>
            </Tax>
            <Tax>
              <Amount CurCode="GBP">10.00</Amount>
              <TaxCode>YQ</TaxCode>
            </Tax>
          </Breakdown>
        </Taxes>
      </TotalPriceDetail>
      <Service>
        <ServiceID>SVC-f47ac10b58cc4372a5670e02b2c3d479</ServiceID>
        <FlightRefs>SEG1</FlightRefs>
      </Service>
      <FareDetail>
        <FareComponent>
          <FareBasisCode>
            <Code>YOWGB</Code>
          </FareBasisCode>
          <CabinType>
            <CabinTypeCode>
              <Code>M</Code>
            </CabinTypeCode>
          </CabinType>
          <SegmentRefs>SEG1</SegmentRefs>
          <FareRules />
        </FareComponent>
      </FareDetail>
      <PassengerRefs>PAX1</PassengerRefs>
    </OfferItem>
    <OfferExpiration>
      <DateTime>2026-07-15T10:30:00Z</DateTime>
    </OfferExpiration>
  </PricedOffer>
  <DataLists>
    <FlightSegmentList>
      <FlightSegment>
        <SegmentKey>SEG1</SegmentKey>
        <Departure>
          <AirportCode>LHR</AirportCode>
          <Date>2026-07-15</Date>
          <Time>10:00</Time>
        </Departure>
        <Arrival>
          <AirportCode>JFK</AirportCode>
          <Date>2026-07-15</Date>
          <Time>13:10</Time>
        </Arrival>
        <MarketingCarrier>
          <AirlineID>AX</AirlineID>
          <FlightNumber>001</FlightNumber>
          <Name>Apex Air</Name>
        </MarketingCarrier>
        <OperatingCarrier>
          <AirlineID>AX</AirlineID>
          <FlightNumber>001</FlightNumber>
        </OperatingCarrier>
        <Equipment>
          <AircraftCode>351</AircraftCode>
        </Equipment>
      </FlightSegment>
    </FlightSegmentList>
    <OriginDestinationList>
      <OriginDestination>
        <OriginDestinationKey>OD1</OriginDestinationKey>
        <DepartureCode>LHR</DepartureCode>
        <ArrivalCode>JFK</ArrivalCode>
        <FlightReferences>SEG1</FlightReferences>
      </OriginDestination>
    </OriginDestinationList>
    <AnonymousTravelerList>
      <AnonymousTraveler>
        <ObjectKey>PAX1</ObjectKey>
        <PTC>ADT</PTC>
        <Quantity>1</Quantity>
      </AnonymousTraveler>
    </AnonymousTravelerList>
  </DataLists>
</IATA_OfferPriceRS>`
  },
  {
    name: 'OrderCreate',
    method: 'POST',
    description: 'Create a new order from a priced offer, capturing passenger details and payment.',
    version: '21.3',
    implemented: true,
    requestXml: `<?xml version="1.0" encoding="UTF-8"?>
<IATA_OrderCreateRQ xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_OrderCreateRQ">
  <Query>
    <OrderItems>
      <OfferItem>
        <OfferRefID>f47ac10b-58cc-4372-a567-0e02b2c3d479</OfferRefID>
        <OfferItemRefID>ITEM-f47ac10b58cc4372a5670e02b2c3d479</OfferItemRefID>
      </OfferItem>
      <ShoppingResponse>
        <ResponseID>a1b2c3d4-e5f6-7890-abcd-ef1234567890</ResponseID>
      </ShoppingResponse>
    </OrderItems>
    <DataLists>
      <PaxList>
        <Pax>
          <PaxID>PAX1</PaxID>
          <PTC>ADT</PTC>
          <Individual>
            <GivenName>JOHN</GivenName>
            <Surname>SMITH</Surname>
            <Birthdate>1985-06-15</Birthdate>
            <GenderCode>M</GenderCode>
          </Individual>
          <ContactInfoRefID>CI1</ContactInfoRefID>
        </Pax>
      </PaxList>
      <ContactInfoList>
        <ContactInfo>
          <ContactInfoID>CI1</ContactInfoID>
          <EmailAddress>
            <EmailAddressText>john.smith@example.com</EmailAddressText>
          </EmailAddress>
          <Phone>
            <PhoneNumber>+44 7700 900123</PhoneNumber>
          </Phone>
        </ContactInfo>
      </ContactInfoList>
    </DataLists>
    <Payments>
      <Payment>
        <Method>
          <PaymentCard>
            <CardHolderName>JOHN SMITH</CardHolderName>
            <CardNumber>
              <PlainCardNumber>4111111111111111</PlainCardNumber>
            </CardNumber>
            <CardTypeCode>VI</CardTypeCode>
            <Expiry>
              <Month>12</Month>
              <Year>2027</Year>
            </Expiry>
            <SeriesCode>
              <Value>123</Value>
            </SeriesCode>
          </PaymentCard>
        </Method>
      </Payment>
    </Payments>
  </Query>
</IATA_OrderCreateRQ>`,
    responseXml: `<?xml version="1.0" encoding="UTF-8"?>
<IATA_OrderCreateRS xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_OrderCreateRS">
  <Document>
    <ReferenceVersion>21.3</ReferenceVersion>
  </Document>
  <Response>
    <Order>
      <OrderID>7890abcd-ef12-3456-7890-abcdef012345</OrderID>
      <BookingRef>
        <BookingEntity>
          <Carrier>
            <AirlineDesigCode>AX</AirlineDesigCode>
          </Carrier>
        </BookingEntity>
        <ID>AXJK42</ID>
      </BookingRef>
      <StatusCode>ISSUED</StatusCode>
      <TotalAmount CurCode="GBP">542.00</TotalAmount>
      <OrderItem>
        <OrderItemID>abc12345-6789-0def-1234-56789abcdef0</OrderItemID>
        <StatusCode>PAYMENT_DONE</StatusCode>
        <FlightRefs>SEG1</FlightRefs>
        <PaxRefID>PAX1</PaxRefID>
        <Price>
          <TotalAmount CurCode="GBP">542.00</TotalAmount>
          <BaseAmount CurCode="GBP">480.00</BaseAmount>
          <Taxes>
            <Total CurCode="GBP">62.00</Total>
          </Taxes>
        </Price>
        <FareDetail>
          <FareComponent>
            <FareBasisCode>
              <Code>YOWGB</Code>
            </FareBasisCode>
            <CabinType>
              <CabinTypeCode>
                <Code>M</Code>
              </CabinTypeCode>
            </CabinType>
            <SegmentRefs>SEG1</SegmentRefs>
          </FareComponent>
        </FareDetail>
      </OrderItem>
      <TicketDocInfo>
        <PaxRefID>PAX1</PaxRefID>
        <TicketDocument>
          <TicketDocNbr>1252345678901</TicketDocNbr>
          <Type>T</Type>
          <ReportingType>BSP</ReportingType>
        </TicketDocument>
      </TicketDocInfo>
    </Order>
    <DataLists>
      <FlightSegmentList>
        <FlightSegment>
          <SegmentKey>SEG1</SegmentKey>
          <Departure>
            <AirportCode>LHR</AirportCode>
            <Date>2026-07-15</Date>
            <Time>10:00</Time>
          </Departure>
          <Arrival>
            <AirportCode>JFK</AirportCode>
            <Date>2026-07-15</Date>
            <Time>13:10</Time>
          </Arrival>
          <MarketingCarrier>
            <AirlineID>AX</AirlineID>
            <FlightNumber>001</FlightNumber>
            <Name>Apex Air</Name>
          </MarketingCarrier>
          <OperatingCarrier>
            <AirlineID>AX</AirlineID>
            <FlightNumber>001</FlightNumber>
          </OperatingCarrier>
          <Equipment>
            <AircraftCode>351</AircraftCode>
          </Equipment>
        </FlightSegment>
      </FlightSegmentList>
      <OriginDestinationList>
        <OriginDestination>
          <OriginDestinationKey>OD1</OriginDestinationKey>
          <DepartureCode>LHR</DepartureCode>
          <ArrivalCode>JFK</ArrivalCode>
          <FlightReferences>SEG1</FlightReferences>
        </OriginDestination>
      </OriginDestinationList>
      <PaxList>
        <Pax>
          <PaxID>PAX1</PaxID>
          <PTC>ADT</PTC>
          <Individual>
            <GivenName>JOHN</GivenName>
            <Surname>SMITH</Surname>
          </Individual>
        </Pax>
      </PaxList>
    </DataLists>
  </Response>
</IATA_OrderCreateRS>`
  },
  {
    name: 'OrderRetrieve',
    method: 'POST',
    description: 'Retrieve the full details of an existing order by booking reference and lead passenger surname.',
    version: '21.3',
    implemented: true,
    requestXml: `<?xml version="1.0" encoding="UTF-8"?>
<IATA_OrderRetrieveRQ xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_OrderRetrieveRQ">
  <Query>
    <Filters>
      <OrderFilter>
        <BookingRef>
          <ID>AXJK42</ID>
        </BookingRef>
        <Pax>
          <Individual>
            <Surname>SMITH</Surname>
          </Individual>
        </Pax>
      </OrderFilter>
    </Filters>
  </Query>
</IATA_OrderRetrieveRQ>`,
    responseXml: `<?xml version="1.0" encoding="UTF-8"?>
<IATA_OrderRetrieveRS xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_OrderRetrieveRS">
  <Document>
    <ReferenceVersion>21.3</ReferenceVersion>
  </Document>
  <Response>
    <Order>
      <OrderID>7890abcd-ef12-3456-7890-abcdef012345</OrderID>
      <BookingRef>
        <BookingEntity>
          <Carrier>
            <AirlineDesigCode>AX</AirlineDesigCode>
          </Carrier>
        </BookingEntity>
        <ID>AXJK42</ID>
      </BookingRef>
      <StatusCode>ISSUED</StatusCode>
      <TotalAmount CurCode="GBP">542.00</TotalAmount>
      <OrderItem>
        <OrderItemID>abc12345-6789-0def-1234-56789abcdef0</OrderItemID>
        <StatusCode>PAYMENT_DONE</StatusCode>
        <FlightRefs>SEG1</FlightRefs>
        <PaxRefID>PAX1</PaxRefID>
        <Price>
          <TotalAmount CurCode="GBP">542.00</TotalAmount>
          <BaseAmount CurCode="GBP">480.00</BaseAmount>
          <Taxes>
            <Total CurCode="GBP">62.00</Total>
          </Taxes>
        </Price>
        <FareDetail>
          <FareComponent>
            <FareBasisCode>
              <Code>YOWGB</Code>
            </FareBasisCode>
            <CabinType>
              <CabinTypeCode>
                <Code>M</Code>
              </CabinTypeCode>
            </CabinType>
            <SegmentRefs>SEG1</SegmentRefs>
          </FareComponent>
        </FareDetail>
      </OrderItem>
      <TicketDocInfo>
        <PaxRefID>PAX1</PaxRefID>
        <TicketDocument>
          <TicketDocNbr>1252345678901</TicketDocNbr>
          <Type>T</Type>
          <ReportingType>BSP</ReportingType>
        </TicketDocument>
      </TicketDocInfo>
    </Order>
    <DataLists>
      <FlightSegmentList>
        <FlightSegment>
          <SegmentKey>SEG1</SegmentKey>
          <Departure>
            <AirportCode>LHR</AirportCode>
            <Date>2026-07-15</Date>
            <Time>10:00</Time>
          </Departure>
          <Arrival>
            <AirportCode>JFK</AirportCode>
            <Date>2026-07-15</Date>
            <Time>13:10</Time>
          </Arrival>
          <MarketingCarrier>
            <AirlineID>AX</AirlineID>
            <FlightNumber>001</FlightNumber>
            <Name>Apex Air</Name>
          </MarketingCarrier>
          <OperatingCarrier>
            <AirlineID>AX</AirlineID>
            <FlightNumber>001</FlightNumber>
          </OperatingCarrier>
          <Equipment>
            <AircraftCode>351</AircraftCode>
          </Equipment>
        </FlightSegment>
      </FlightSegmentList>
      <OriginDestinationList>
        <OriginDestination>
          <OriginDestinationKey>OD1</OriginDestinationKey>
          <DepartureCode>LHR</DepartureCode>
          <ArrivalCode>JFK</ArrivalCode>
          <FlightReferences>SEG1</FlightReferences>
        </OriginDestination>
      </OriginDestinationList>
      <PaxList>
        <Pax>
          <PaxID>PAX1</PaxID>
          <PTC>ADT</PTC>
          <Individual>
            <GivenName>JOHN</GivenName>
            <Surname>SMITH</Surname>
          </Individual>
        </Pax>
      </PaxList>
    </DataLists>
  </Response>
</IATA_OrderRetrieveRS>`
  },
  {
    name: 'OrderChange',
    method: 'POST',
    description: 'Modify an existing order — change flight, add services, or update passenger data.',
    version: '21.3',
    implemented: false
  },
  {
    name: 'OrderCancel',
    method: 'POST',
    description: 'Cancel an order or individual order items and initiate any applicable refunds.',
    version: '21.3',
    implemented: false
  },
  {
    name: 'SeatAvailability',
    method: 'POST',
    description: 'Return the seat map and availability for a given flight and cabin class.',
    version: '21.3',
    implemented: true,
    requestXml: `<?xml version="1.0" encoding="UTF-8"?>
<IATA_SeatAvailabilityRQ xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_SeatAvailabilityRQ">
  <Query>
    <OriginDestCriteria>
      <OfferRefID>f47ac10b-58cc-4372-a567-0e02b2c3d479</OfferRefID>
    </OriginDestCriteria>
  </Query>
  <Travelers>
    <Traveler>
      <AnonymousTraveler>
        <PTC>ADT</PTC>
        <Quantity>1</Quantity>
      </AnonymousTraveler>
    </Traveler>
  </Travelers>
</IATA_SeatAvailabilityRQ>`,
    responseXml: `<?xml version="1.0" encoding="UTF-8"?>
<IATA_SeatAvailabilityRS xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_SeatAvailabilityRS">
  <Document>
    <ReferenceVersion>21.3</ReferenceVersion>
  </Document>
  <Response>
    <SeatAvailability>
      <Flights>
        <Flight>
          <SegmentRefs>SEG1</SegmentRefs>
          <CabinList>
            <Cabin>
              <CabinCode>M</CabinCode>
              <CabinName>Economy</CabinName>
              <DeckCode>Main</DeckCode>
              <FirstRowNumber>20</FirstRowNumber>
              <LastRowNumber>45</LastRowNumber>
              <ColumnList>
                <Column>
                  <Position>A</Position>
                  <SeatCharacteristicCode>W</SeatCharacteristicCode>
                </Column>
                <Column>
                  <Position>B</Position>
                  <SeatCharacteristicCode>M</SeatCharacteristicCode>
                </Column>
                <Column>
                  <Position>C</Position>
                  <SeatCharacteristicCode>A</SeatCharacteristicCode>
                </Column>
              </ColumnList>
              <RowList>
                <Row>
                  <RowNumber>20</RowNumber>
                  <Seat>
                    <Column>A</Column>
                    <SeatNumber>20A</SeatNumber>
                    <OccupationStatusCode>F</OccupationStatusCode>
                    <SeatCharacteristicCode>W</SeatCharacteristicCode>
                    <OfferRef>
                      <OfferRefID>seat-offer-001</OfferRefID>
                      <UnitPrice>
                        <TotalAmount CurCode="GBP">35.00</TotalAmount>
                        <BaseAmount CurCode="GBP">30.00</BaseAmount>
                        <Taxes>
                          <Total CurCode="GBP">5.00</Total>
                        </Taxes>
                      </UnitPrice>
                    </OfferRef>
                  </Seat>
                  <Seat>
                    <Column>B</Column>
                    <SeatNumber>20B</SeatNumber>
                    <OccupationStatusCode>O</OccupationStatusCode>
                    <SeatCharacteristicCode>M</SeatCharacteristicCode>
                  </Seat>
                  <Seat>
                    <Column>C</Column>
                    <SeatNumber>20C</SeatNumber>
                    <OccupationStatusCode>F</OccupationStatusCode>
                    <SeatCharacteristicCode>A</SeatCharacteristicCode>
                  </Seat>
                </Row>
              </RowList>
            </Cabin>
          </CabinList>
        </Flight>
      </Flights>
    </SeatAvailability>
    <DataLists>
      <FlightSegmentList>
        <FlightSegment>
          <SegmentKey>SEG1</SegmentKey>
          <Departure>
            <AirportCode>LHR</AirportCode>
            <Date>2026-07-15</Date>
            <Time>10:00</Time>
          </Departure>
          <Arrival>
            <AirportCode>JFK</AirportCode>
            <Date>2026-07-15</Date>
            <Time>13:10</Time>
          </Arrival>
          <MarketingCarrier>
            <AirlineID>AX</AirlineID>
            <FlightNumber>001</FlightNumber>
            <Name>Apex Air</Name>
          </MarketingCarrier>
          <OperatingCarrier>
            <AirlineID>AX</AirlineID>
            <FlightNumber>001</FlightNumber>
          </OperatingCarrier>
          <Equipment>
            <AircraftCode>351</AircraftCode>
          </Equipment>
        </FlightSegment>
      </FlightSegmentList>
      <OriginDestinationList>
        <OriginDestination>
          <OriginDestinationKey>OD1</OriginDestinationKey>
          <DepartureCode>LHR</DepartureCode>
          <ArrivalCode>JFK</ArrivalCode>
          <FlightReferences>SEG1</FlightReferences>
        </OriginDestination>
      </OriginDestinationList>
    </DataLists>
  </Response>
</IATA_SeatAvailabilityRS>`
  },
  {
    name: 'ServiceList',
    method: 'POST',
    description: 'Return the catalogue of ancillary services available for an offer or order.',
    version: '21.3',
    implemented: true,
    requestXml: `<?xml version="1.0" encoding="UTF-8"?>
<IATA_ServiceListRQ xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_ServiceListRQ">
  <Query>
    <SelectionCriteria>
      <OfferRef>
        <OfferRefID>f47ac10b-58cc-4372-a567-0e02b2c3d479</OfferRefID>
      </OfferRef>
    </SelectionCriteria>
    <CabinPreferences>
      <CabinType>
        <CabinTypeCode>
          <Code>M</Code>
        </CabinTypeCode>
      </CabinType>
    </CabinPreferences>
  </Query>
  <Travelers>
    <Traveler>
      <AnonymousTraveler>
        <PTC>ADT</PTC>
        <Quantity>1</Quantity>
      </AnonymousTraveler>
    </Traveler>
  </Travelers>
</IATA_ServiceListRQ>`,
    responseXml: `<?xml version="1.0" encoding="UTF-8"?>
<IATA_ServiceListRS xmlns="http://www.iata.org/IATA/2015/00/2021.3/IATA_ServiceListRS">
  <Document>
    <ReferenceVersion>21.3</ReferenceVersion>
  </Document>
  <Response>
    <ServiceList>
      <ALaCarteOffer>
        <Owner>AX</Owner>
        <ALaCarteOfferItem>
          <OfferItemID>SLI-VGML</OfferItemID>
          <Eligibility>
            <FlightAssociationType>All</FlightAssociationType>
            <PaxAssociationType>All</PaxAssociationType>
          </Eligibility>
          <Service>
            <ServiceID>SVC-VGML</ServiceID>
            <Name>Vegetarian Meal</Name>
            <ServiceCode>
              <Code>VGML</Code>
              <ServiceType>SSR</ServiceType>
            </ServiceCode>
            <ServiceGroup>
              <Code>MEAL</Code>
            </ServiceGroup>
          </Service>
          <UnitPriceDetail>
            <TotalAmount>
              <SimpleCurrencyPrice CurCode="GBP">0.00</SimpleCurrencyPrice>
            </TotalAmount>
          </UnitPriceDetail>
        </ALaCarteOfferItem>
        <ALaCarteOfferItem>
          <OfferItemID>SLI-WCHR</OfferItemID>
          <Eligibility>
            <FlightAssociationType>All</FlightAssociationType>
            <PaxAssociationType>All</PaxAssociationType>
          </Eligibility>
          <Service>
            <ServiceID>SVC-WCHR</ServiceID>
            <Name>Wheelchair Assistance</Name>
            <ServiceCode>
              <Code>WCHR</Code>
              <ServiceType>SSR</ServiceType>
            </ServiceCode>
            <ServiceGroup>
              <Code>ACCESSIBILITY</Code>
            </ServiceGroup>
          </Service>
          <UnitPriceDetail>
            <TotalAmount>
              <SimpleCurrencyPrice CurCode="GBP">0.00</SimpleCurrencyPrice>
            </TotalAmount>
          </UnitPriceDetail>
        </ALaCarteOfferItem>
      </ALaCarteOffer>
    </ServiceList>
    <DataLists>
      <ServiceDefinitionList>
        <ServiceDefinition>
          <ServiceDefinitionID>SD-VGML</ServiceDefinitionID>
          <Name>Vegetarian Meal</Name>
          <Desc>Vegetarian Meal</Desc>
          <ServiceCode>
            <Code>VGML</Code>
            <ServiceType>SSR</ServiceType>
          </ServiceCode>
          <Category>MEAL</Category>
        </ServiceDefinition>
        <ServiceDefinition>
          <ServiceDefinitionID>SD-WCHR</ServiceDefinitionID>
          <Name>Wheelchair Assistance</Name>
          <Desc>Wheelchair Assistance</Desc>
          <ServiceCode>
            <Code>WCHR</Code>
            <ServiceType>SSR</ServiceType>
          </ServiceCode>
          <Category>ACCESSIBILITY</Category>
        </ServiceDefinition>
      </ServiceDefinitionList>
    </DataLists>
  </Response>
</IATA_ServiceListRS>`
  }
];

@Component({
  selector: 'app-ndc',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './ndc.html',
  styleUrl: './ndc.css'
})
export class NdcComponent {
  readonly endpoints = NDC_ENDPOINTS;
}
