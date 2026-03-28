# Schedule domain

The Schedule domain stores the operational flight schedule for Apex Air. Schedules are imported in bulk from an IATA SSIM Chapter 7 file via the Operations API. The `POST /v1/schedules/ssim` endpoint on the Operations API parses the SSIM file, converts each Type 3 leg record into the season schedule JSON format, and forwards the complete payload to the Schedule MS `POST /v1/schedules`, which atomically replaces all existing schedule records with the new set.

A pre-built JSON schedule payload for the 2026 season is available at `res/schedules/flight-schedule-2026-payloads.json` and can be posted directly to the Schedule MS.

## Data schema — `schedule.FlightSchedule`

| Column | Type | Nullable | Default | Key | Notes |
|---|---|---|---|---|---|
| ScheduleId | UNIQUEIDENTIFIER | No | NEWID() | PK | |
| FlightNumber | VARCHAR(10) | No | | | e.g. `AX001` |
| Origin | CHAR(3) | No | | | IATA airport code |
| Destination | CHAR(3) | No | | | IATA airport code |
| DepartureTime | TIME | No | | | Local time at origin airport |
| ArrivalTime | TIME | No | | | Local time at destination airport |
| ArrivalDayOffset | TINYINT | No | 0 | | `0` = same calendar day; `1` = next day at destination |
| DaysOfWeek | TINYINT | No | | | Bitmask: Mon=1, Tue=2, Wed=4, Thu=8, Fri=16, Sat=32, Sun=64; daily = 127 |
| AircraftType | VARCHAR(4) | No | | | IATA 4-char code, e.g. `A351`, `B789`, `A339` |
| ValidFrom | DATE | No | | | First operating date (inclusive) |
| ValidTo | DATE | No | | | Last operating date (inclusive) |
| FlightsCreated | INT | No | 0 | | Reserved — set to `0` at import time |
| CreatedAt | DATETIME2 | No | SYSUTCDATETIME() | | |
| CreatedBy | VARCHAR(100) | No | | | Identity reference of the operations user who submitted the import |
| UpdatedAt | DATETIME2 | No | SYSUTCDATETIME() | | |

> **Indexes:** `IX_FlightSchedule_FlightNumber` on `(FlightNumber, ValidFrom, ValidTo)`.
> **DaysOfWeek bitmask:** Operating days are encoded as a bitfield (ISO week order: Mon–Sun). A daily flight uses value `127` (all seven bits set); Mon/Wed/Fri uses `21` (bits 1+4+16). This encoding enables efficient date enumeration without a supporting day-of-week lookup table.

## Season schedule JSON payload format

The season schedule payload is the canonical JSON format accepted by the Schedule MS `POST /v1/schedules` endpoint and produced by the Operations API after parsing a SSIM file. A reference payload for the 2026 season is at `res/schedules/flight-schedule-2026-payloads.json`.

```json
{
  "header": {
    "standard": "IATA",
    "airlineCode": "AX",
    "seasonStart": "2026-01-01",
    "seasonEnd": "2026-12-31",
    "fileType": "SCHED",
    "fileCreationDate": "2026-03-26"
  },
  "carriers": [
    {
      "airlineCode": "AX",
      "seasonCode": "01W",
      "validFrom": "2026-01-01",
      "validTo": "2026-12-31",
      "schedules": [
        {
          "flightNumber": "AX001",
          "origin": "LHR",
          "destination": "JFK",
          "departureTime": "08:00",
          "arrivalTime": "11:10",
          "arrivalDayOffset": 0,
          "daysOfWeek": 127,
          "aircraftType": "A351",
          "validFrom": "2026-01-01",
          "validTo": "2026-12-31",
          "createdBy": "schedule-import"
        }
      ]
    }
  ],
  "recordCount": 1
}
```

## SSIM import sequence diagram

```mermaid
sequenceDiagram
    actor OpsUser as Operations User
    participant OpsApp as Ops Admin App
    participant OpsAPI as Operations API
    participant ScheduleMS as Schedule [MS]

    OpsUser->>OpsApp: Upload SSIM file
    OpsApp->>OpsAPI: POST /v1/schedules/ssim (text/plain body, ?createdBy=)
    OpsAPI->>OpsAPI: SsimParser.Parse — split lines, extract Type 2 carrier header, process Type 3 scheduled-passenger records
    OpsAPI->>OpsAPI: Build season schedule JSON payload (header + carriers[].schedules[])
    OpsAPI->>ScheduleMS: POST /v1/schedules (application/json — season schedule payload)
    ScheduleMS->>ScheduleMS: Validate all schedule entries
    ScheduleMS->>ScheduleMS: Delete all existing FlightSchedule records
    loop For each schedule in payload
        ScheduleMS->>ScheduleMS: FlightSchedule.Create — insert into [schedule].[FlightSchedule]
    end
    ScheduleMS-->>OpsAPI: 200 OK — { imported, deleted, schedules: [{ scheduleId, flightNumber, origin, destination, validFrom, validTo, operatingDateCount }] }
    OpsAPI-->>OpsApp: 200 OK — import summary
    OpsApp-->>OpsUser: N schedules imported, M previous records replaced
```

*Ref: SSIM import flow — the Operations API owns SSIM parsing and payload construction; the Schedule MS owns persistence and atomically replaces all existing records on each import.*

## SSIM format reference

### What is SSIM?

SSIM (Standard Schedules Information Manual) is an IATA standard defining a plain ASCII flat-file format for airline schedule exchange. It is used industry-wide for bulk timetable distribution to GDS systems, airport slot coordinators, codeshare partners, and other downstream consumers.

The file format is fixed-width and positional — every character position in every record has a defined meaning. Records are exactly **200 characters wide**, space-padded, and terminated with CRLF. The conventional file extension is `.ssim`.

### Record types

| Record type | First char | Purpose |
|---|---|---|
| Type 1 | `1` | Transmission header — sender, season window, file creation date |
| Type 2 | `2` | Carrier header — airline designator and IATA season code |
| Type 3 | `3` | Flight leg record — one per `FlightSchedule` row; carries the operating pattern |
| Type 5 | `5` | Trailer — count of Type 3 records for validation |

The Operations API `SsimParser` processes Type 2 records to extract carrier and season metadata, and Type 3 records with service type `Y` (scheduled passenger) to build schedule definitions. All other record types are silently skipped.

### Type 3 positional layout

| Positions | Width | Content | Example |
|---|---|---|---|
| 1 | 1 | Record type | `3` |
| 2 | 1 | Operational suffix (space = active) | ` ` |
| 3–4 | 2 | Airline IATA designator | `AX` |
| 5 | 1 | Space | ` ` |
| 6–9 | 4 | Flight number, zero-padded | `0001` |
| 10 | 1 | Space | ` ` |
| 11 | 1 | Service type (`Y` = scheduled passenger) | `Y` |
| 12 | 1 | Space | ` ` |
| 13–20 | 8 | Period start `YYYYMMDD` | `20260101` |
| 21 | 1 | Space | ` ` |
| 22–29 | 8 | Period end `YYYYMMDD` | `20261231` |
| 30–31 | 2 | Itinerary variation (unused for non-stop) | `  ` |
| 32–38 | 7 | Days-of-week mask (see below) | `1234567` |
| 39 | 1 | Space | ` ` |
| 40–42 | 3 | Departure station IATA code | `LHR` |
| 43–46 | 4 | Departure time local `HHMM` | `0800` |
| 47–49 | 3 | Departure UTC offset | `+00` |
| 50–53 | 4 | Arrival time local `HHMM` | `1110` |
| 54 | 1 | Arrival day offset (`0` = same day, `1` = next day) | `0` |
| 55 | 1 | Space | ` ` |
| 56–58 | 3 | Destination station IATA code | `JFK` |
| 59–61 | 3 | Spaces | `   ` |
| 62–64 | 3 | Equipment IATA type code (first 3 chars) | `A35` |
| 65 | 1 | Space | ` ` |
| 66–67 | 2 | Operating carrier code | `AX` |
| 68–200 | 133 | Space-padded to record width | |

All field positions are 0-indexed in `SsimParser`. A line must be at least 68 characters long to be processed.

### Days-of-week encoding

The 7-character days-of-week field uses **absolute positions**, not sequential digits. Position 1 always represents Monday, position 7 always represents Sunday.

| DaysOfWeek bitmask bit | SSIM position | Day |
|---|---|---|
| bit 0 (value 1) | position 1 | Monday |
| bit 1 (value 2) | position 2 | Tuesday |
| bit 2 (value 4) | position 3 | Wednesday |
| bit 3 (value 8) | position 4 | Thursday |
| bit 4 (value 16) | position 5 | Friday |
| bit 5 (value 32) | position 6 | Saturday |
| bit 6 (value 64) | position 7 | Sunday |

Examples:

| DaysOfWeek value | SSIM string | Meaning |
|---|---|---|
| `127` | `1234567` | Daily |
| `21` | `1 3 5  ` | Mon, Wed, Fri |
| `96` | `     67` | Sat, Sun |
| `1` | `1      ` | Monday only |

### Flight number round-trip

The SSIM numeric field is a 4-digit zero-padded integer (e.g. `0001`). The parser strips leading zeros and pads to a minimum of 3 digits: `AX` + `0001` → `AX001`; `AX` + `1001` → `AX1001`.

### Aircraft type code mapping

SSIM uses 3-character equipment codes; the system stores 4-character IATA codes. The parser maps known codes:

| SSIM code | Internal code |
|---|---|
| `351` | `A351` |
| `789` | `B789` |
| `339` | `A339` |
| `788` | `B788` |
| `77W` | `B77W` |
| `744` | `B744` |
| `333` | `A333` |
| `359` | `A359` |

Unknown codes are passed through unchanged.

### Example SSIM file

```
1IATA  AX              20260101 20261231AX  SCHED  20260326
2AX  01W20260101 20261231
3 AX 0001 Y 20260101 20261231  1234567 LHR0800+001110 0 JFK   A35 AX
3 AX 0002 Y 20260101 20261231  1234567 JFK1300+000115 1 LHR   A35 AX
5       2
```

A full 2026 season SSIM file is available at `res/schedules/flight-schedule-2026.ssim`.
