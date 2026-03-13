# new-api

Create a new Azure Functions microservice API for the Reservation System, based on the TemplateApi clean architecture pattern.

## Usage

```
/new-api <ApiName> <EntityName> <ApiType>
```

- `<ApiName>` — the domain name, PascalCase (e.g. `Offer`, `Order`, `Payment`, `Seat`)
- `<EntityName>` — the primary entity for this domain, PascalCase (e.g. `Offer`, `Order`, `PaymentRecord`, `SeatMap`)
- `<ApiType>` — the category directory for this API, either `Microservice` or `Orchestration`

`<ApiName>` and `<EntityName>` are often the same word but can differ (e.g. API `Payment`, entity `PaymentRecord`).

`<ApiType>` controls which top-level grouping directory the API is placed under alongside `Template/`. Use `Microservice` for standalone domain APIs that own their data, and `Orchestration` for APIs that coordinate across multiple microservices.

**Examples:**
- `/new-api Offer Offer Microservice`
- `/new-api Booking BookingRecord Orchestration`

---

## What to do

You are creating a new API project by copying and adapting the TemplateApi structure. Work through every step below in order.

### Step 1 — Derive naming variants

From the three arguments, derive all the naming forms you will need throughout the files:

| Variant | Example (Offer / Offer / Microservice) | Example (Payment / PaymentRecord / Orchestration) |
|---|---|---|
| Api name PascalCase | `Offer` | `Payment` |
| Entity name PascalCase | `Offer` | `PaymentRecord` |
| Entity name camelCase | `offer` | `paymentRecord` |
| Entity name kebab-case | `offer` | `payment-record` |
| Entity name lower (schema) | `offer` | `payment` |
| Table name (plural PascalCase) | `Offers` | `PaymentRecords` |
| Api type directory | `Microservice` | `Orchestration` |

### Step 2 — Create the project directory

Create the directory under the appropriate `<ApiType>` grouping folder:

```
src/API/<ApiType>/<ApiName>/<ApiName>Api/
```

e.g. `src/API/Microservice/Offer/OfferApi/` or `src/API/Orchestration/Booking/BookingApi/`

The `<ApiType>` directory (`Microservice` or `Orchestration`) will be created automatically if it does not yet exist — it sits at the same level as the `Template/` directory.

### Step 3 — Create all files

Create every file listed below. Base each file on the corresponding TemplateApi file at `src/API/Template/TemplateApi/`, substituting:

- Every occurrence of `Template` → `<ApiName>` (in namespaces, class names, folder paths)
- Every occurrence of `TemplateItem` → `<EntityName>` (in class names, method names, variable names)
- Every occurrence of `templateItem` → entity camelCase variant
- Every occurrence of `template-items` → entity kebab-case plural in route paths
- Every occurrence of `template` in SQL schema names → entity lower-case singular
- Every occurrence of `Items` in SQL table names → entity plural PascalCase
- The project GUID in the `.csproj` and `.sln` entries → generate a new random GUID

#### Files to create

```
<ApiName>Api/
│
├── <ApiName>Api.csproj
├── Program.cs
├── host.json
├── local.settings.json
│
├── Domain/
│   ├── Entities/
│   │   └── <EntityName>.cs
│   ├── ValueObjects/
│   │   └── <EntityName>Metadata.cs
│   └── Repositories/
│       └── I<EntityName>Repository.cs
│
├── Application/
│   └── UseCases/
│       ├── Get<EntityName>/
│       │   ├── Get<EntityName>Query.cs
│       │   └── Get<EntityName>Handler.cs
│       ├── GetAll<EntityName>s/
│       │   ├── GetAll<EntityName>sQuery.cs
│       │   └── GetAll<EntityName>sHandler.cs
│       ├── Create<EntityName>/
│       │   ├── Create<EntityName>Command.cs
│       │   └── Create<EntityName>Handler.cs
│       └── Delete<EntityName>/
│           ├── Delete<EntityName>Command.cs
│           └── Delete<EntityName>Handler.cs
│
├── Infrastructure/
│   ├── Configuration/
│   │   └── DatabaseOptions.cs
│   └── Persistence/
│       ├── SqlConnectionFactory.cs
│       ├── Sql<EntityName>Repository.cs
│       └── Scripts/
│           └── schema.sql
│
├── Models/
│   ├── Requests/
│   │   └── Create<EntityName>Request.cs
│   ├── Responses/
│   │   └── <EntityName>Response.cs
│   ├── Database/
│   │   ├── <EntityName>Record.cs
│   │   └── JsonFields/
│   │       └── <EntityName>Attributes.cs
│   └── Mappers/
│       └── <EntityName>Mapper.cs
│
└── Functions/
    ├── HelloWorldFunction.cs
    ├── HealthCheckFunction.cs
    └── <EntityName>Function.cs
```

**Note:** `DatabaseOptions.cs` and `SqlConnectionFactory.cs` are identical across all APIs — copy them verbatim, changing only the namespace.

### Step 4 — Domain-specific entity fields

The TemplateItem entity uses generic `Name`, `Status`, and `Metadata` fields. For the new domain, **replace these with fields that make sense for that domain**. Use your knowledge of airline reservation systems and the architecture documentation at `architecture/` to choose appropriate fields.

Examples:
- `Offer` entity → `FlightNumber`, `Origin`, `Destination`, `DepartureAt`, `FareClass`, `TotalPrice`, `Currency`, `Status`
- `Order` entity → `OrderReference`, `PassengerCount`, `TotalAmount`, `Currency`, `Status`, `BookedAt`
- `Seat` entity → `FlightNumber`, `SeatNumber`, `CabinClass`, `IsAvailable`, `Attributes` (JSON — holds seat features like window/aisle/extra-legroom from the seatmap JSON files)
- `Schedule` entity → `FlightNumber`, `Origin`, `Destination`, `DepartureTime`, `ArrivalTime`, `AircraftType`, `OperatingDays`

Adapt the `Metadata`/`Attributes` JSON field to hold fields that are likely to be extended over time or vary per flight type — keep stable, queryable fields as proper SQL columns.

Update the SQL schema, Dapper record, JSON field object, domain entity, value objects, request/response models, and mapper consistently.

### Step 5 — Update the SQL schema

In `Infrastructure/Persistence/Scripts/schema.sql`:
- Schema name: `[<entity-lower>]` (e.g. `[offer]`, `[order]`)
- Table name: `[<entity-lower>].[<EntityPlural>]` (e.g. `[offer].[Offers]`, `[order].[Orders]`)
- Columns should reflect the domain-specific fields chosen in Step 4
- Include appropriate indexes for the most common query patterns (status, date ranges, flight number where relevant)
- Keep the `ISJSON` check constraint on any JSON columns

### Step 6 — Update the HTTP routes

In `Functions/<EntityName>Function.cs`, set routes as:
- `GET    v1/<entity-kebab-plural>`          (list)
- `GET    v1/<entity-kebab-plural>/{id:guid}` (get by id)
- `POST   v1/<entity-kebab-plural>`          (create)
- `DELETE v1/<entity-kebab-plural>/{id:guid}` (delete)

e.g. for Offer: `v1/offers`, `v1/offers/{id:guid}`

### Step 7 — Register the new project in the solution file

Add a new `Project(...)...EndProject` block to `src/API/ReservationSystem.sln` using the same format as the existing TemplateApi entry. Generate a new GUID for the project. Also add the corresponding lines to the `ProjectConfigurationPlatforms` section.

The path should be relative to the `.sln` file location:
```
<ApiType>/<ApiName>/<ApiName>Api/<ApiName>Api.csproj
```

e.g. `Microservice/Offer/OfferApi/OfferApi.csproj` or `Orchestration/Booking/BookingApi/BookingApi.csproj`

### Step 8 — Create the GitHub Actions build workflow

Create the file `.github/workflows/<api-name-lowercase>-api-build.yml` (e.g. `offer-api-build.yml`, `booking-api-build.yml`).

The workflow must:
- Be named `<ApiName> API Build`
- Trigger on `push` and `pull_request` to `main` and `master`, path-filtered to:
  - `src/API/<ApiType>/<ApiName>/**`
  - `src/API/ReservationSystem.sln`
  - `.github/workflows/<api-name-lowercase>-api-build.yml`
- Have a single job named `build` running on `ubuntu-latest` with these steps:
  1. `actions/checkout@v4`
  2. `actions/setup-dotnet@v4` with `dotnet-version: '8.0.x'`
  3. `dotnet restore` targeting `src/API/<ApiType>/<ApiName>/<ApiName>Api/<ApiName>Api.csproj`
  4. `dotnet build` with `--configuration Release --no-restore`
  5. `dotnet publish` with `--configuration Release --no-build --output ./publish/<api-name-lowercase>-api`
  6. `actions/upload-artifact@v4` — artifact name `<api-name-lowercase>-api`, path `./publish/<api-name-lowercase>-api`, `retention-days: 7`

Use the existing `.github/workflows/template-api-build.yml` as a reference for the exact YAML structure.

### Step 9 — Verify the structure

After creating all files, print a tree of the new project directory so the user can confirm everything looks right before committing.

### Step 10 — Commit and push

Stage all new files, including the workflow file. Commit with a message following this format:

```
Add <ApiName>Api — clean architecture scaffold for <domain description>

<2–3 bullet points describing the domain entity fields chosen and any
notable decisions made for this specific domain>
```

Push to the current working branch.

---

## Rules and constraints

- **Never modify** `src/API/Template/` — it is the reference template, not a working API
- **`<ApiType>` must be exactly** `Microservice` or `Orchestration` — reject any other value and ask the user to correct it
- **Namespaces** must follow the pattern `ReservationSystem.<ApiName>.<ApiName>Api.<Layer>.<SubLayer>` — the `<ApiType>` directory is a filesystem grouping only and does not appear in namespaces
- **No new NuGet packages** beyond what TemplateApi already uses unless the domain genuinely requires it
- **Static mappers only** — do not introduce AutoMapper or other mapping libraries
- **No MediatR** — handlers are registered directly in DI as scoped services
- All **JSON property names** in request/response models use camelCase (`[JsonPropertyName]`)
- SQL column names use **PascalCase**, C# properties use PascalCase — Dapper matches them directly
- The **`local.settings.json`** connection string should be updated to use the database name `ReservationSystem` (same shared Azure SQL instance, different schema)
- Do **not** create a README or additional documentation files unless explicitly asked
