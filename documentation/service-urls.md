# Service URLs — live environments

All services are deployed to Azure App Service (Azure Functions v4, .NET 8 isolated worker process).

---

## Orchestration APIs

| Service | Base URL | Region |
|---------|----------|--------|
| Retail API | TBC | TBC |
| Loyalty API | `https://reservation-system-db-api-loyalty-gufra2fxfdd2eka6.uksouth-01.azurewebsites.net` | UK South |
| Operations API | `https://reservation-system-db-api-operations-gzfhekfvawaubkbs.uksouth-01.azurewebsites.net` | UK South |
| Disruption API | TBC | TBC |
| Admin API | `https://reservation-system-db-api-admin-ageucwaad3axbxhm.uksouth-01.azurewebsites.net` | UK South |

---

## Microservices

| Service | Base URL | Region |
|---------|----------|--------|
| Offer MS | `https://reservation-system-db-microservice-offer-dnfdbebdezemaghp.uksouth-01.azurewebsites.net` | UK South |
| Order MS | TBC | TBC |
| Payment MS | `https://reservation-system-db-microservice-payment-f3amf7a6bmauhjd6.uksouth-01.azurewebsites.net` | UK South |
| Delivery MS | TBC | TBC |
| Customer MS | `https://reservation-system-db-microservice-customer-axdydza6brbkc0ck.uksouth-01.azurewebsites.net` | UK South |
| Seat MS | TBC | TBC |
| Bag MS | TBC | TBC |
| Schedule MS | `https://reservation-system-db-microservice-schedule-cvbebgdqgcbpeeb7.uksouth-01.azurewebsites.net` | UK South |
| Identity MS | `https://reservation-system-db-microservice-identity-dwdegsahhngkbvgv.uksouth-01.azurewebsites.net` | UK South |
| User MS | `https://reservation-system-db-microservice-user-frhedyd4dcc6aya8.uksouth-01.azurewebsites.net` | UK South |

---

## Standard endpoints

All services expose the following standard endpoints relative to their base URL:

| Endpoint | Description |
|----------|-------------|
| `GET /api/v1/health` | Health check — returns 200 OK if the service is running |
| `GET /api/swagger.json` | OpenAPI specification |

---

## Configuration keys

Each orchestration API reads downstream base URLs from Azure App Settings. The keys follow the pattern `<ServiceName>:BaseUrl`. If a key is absent the `BaseAddress` is `null` and the service client will throw on first use.

| API | Config key | Target service |
|-----|-----------|----------------|
| Operations API | `ScheduleMs:BaseUrl` | Schedule MS |
| Operations API | `ScheduleMs:HostKey` | Schedule MS — Azure Function host key |
| Operations API | `OfferMs:BaseUrl` | Offer MS |
| Operations API | `OfferMs:HostKey` | Offer MS — Azure Function host key |
| Admin API | `UserMs:BaseUrl` | User MS |
| Admin API | `UserMs:HostKey` | User MS — Azure Function host key |
| Loyalty API | `IdentityMs:BaseUrl` | Identity MS |
| Loyalty API | `CustomerMs:BaseUrl` | Customer MS |
| Loyalty API | `UserMs:JwtSecret` | Staff JWT signing secret (Base64-encoded 256-bit key) — used to validate staff tokens on admin endpoints |
| Loyalty API | `UserMs:JwtIssuer` | Staff JWT issuer (default: `apex-air-user`) |
| Loyalty API | `UserMs:JwtAudience` | Staff JWT audience (default: `apex-air-reservation`) |
| Retail API | `OfferMs:BaseUrl` | Offer MS |
| Retail API | `OrderMs:BaseUrl` | Order MS |
| Retail API | `PaymentMs:BaseUrl` | Payment MS |
| Retail API | `DeliveryMs:BaseUrl` | Delivery MS |
| Retail API | `SeatMs:BaseUrl` | Seat MS |
| Retail API | `BagMs:BaseUrl` | Bag MS |
| Disruption API | `OfferMs:BaseUrl` | Offer MS |
| Disruption API | `OrderMs:BaseUrl` | Order MS |
| Disruption API | `DeliveryMs:BaseUrl` | Delivery MS |
| Disruption API | `CustomerMs:BaseUrl` | Customer MS |
| Disruption API | `PaymentMs:BaseUrl` | Payment MS |
