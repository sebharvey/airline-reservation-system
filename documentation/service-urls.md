# Service URLs — live environments

All services are deployed to Azure App Service (Azure Functions v4, .NET 8 isolated worker process).

---

## Orchestration APIs

| Service | Base URL | Region |
|---------|----------|--------|
| Retail API | `https://reservation-system-db-api-retail-aqasakbxcje0a6eh.uksouth-01.azurewebsites.net` | UK South |
| Loyalty API | `https://reservation-system-db-api-loyalty-gufra2fxfdd2eka6.uksouth-01.azurewebsites.net` | UK South |
| Operations API | `https://reservation-system-db-api-operations-gzfhekfvawaubkbs.uksouth-01.azurewebsites.net` | UK South |
| Admin API | `https://reservation-system-db-api-admin-ageucwaad3axbxhm.uksouth-01.azurewebsites.net` | UK South |

---

## Simulators

| Service | Base URL | Region |
|---------|----------|--------|
| Timatic Simulator | `https://reservation-system-simulator-timatic-h0guaxfvgaengdeh.uksouth-01.azurewebsites.net` | UK South |

---

## Microservices

| Service | Base URL | Region |
|---------|----------|--------|
| Offer MS | `https://reservation-system-db-microservice-offer-dnfdbebdezemaghp.uksouth-01.azurewebsites.net` | UK South |
| Order MS | `https://reservation-system-db-microservice-order-cnc3fpdzfucbhudc.uksouth-01.azurewebsites.net` | UK South |
| Payment MS | `https://reservation-system-db-microservice-payment-f3amf7a6bmauhjd6.uksouth-01.azurewebsites.net` | UK South |
| Delivery MS | `https://reservation-system-db-microservice-delivery-ehe2f4c3dybehwat.uksouth-01.azurewebsites.net` | UK South |
| Customer MS | `https://reservation-system-db-microservice-customer-axdydza6brbkc0ck.uksouth-01.azurewebsites.net` | UK South |
| Ancillary MS | `https://reservation-system-microservice-ancillary-dkdfdjfba9fcbvfk.uksouth-01.azurewebsites.net` | UK South |
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
| Operations API | `OrderMs:BaseUrl` | Order MS |
| Operations API | `OrderMs:HostKey` | Order MS — Azure Function host key |
| Operations API | `DeliveryMs:BaseUrl` | Delivery MS |
| Operations API | `DeliveryMs:HostKey` | Delivery MS — Azure Function host key |
| Operations API | `CustomerMs:BaseUrl` | Customer MS |
| Operations API | `CustomerMs:HostKey` | Customer MS — Azure Function host key |
| Operations API | `PaymentMs:BaseUrl` | Payment MS |
| Operations API | `AncillaryMs:BaseUrl` | Ancillary MS |
| Operations API | `AncillaryMs:HostKey` | Ancillary MS — Azure Function host key |
| Admin API | `UserMs:BaseUrl` | User MS |
| Admin API | `UserMs:HostKey` | User MS — Azure Function host key |
| Loyalty API | `IdentityMs:BaseUrl` | Identity MS |
| Loyalty API | `CustomerMs:BaseUrl` | Customer MS |
| Loyalty API | `UserMs:JwtSecret` | Staff JWT signing secret (Base64-encoded 256-bit key) — used to validate staff tokens on admin endpoints |
| Loyalty API | `UserMs:JwtIssuer` | Staff JWT issuer (default: `apex-air-user`) |
| Loyalty API | `UserMs:JwtAudience` | Staff JWT audience (default: `apex-air-reservation`) |
| Retail API | `OfferMs:BaseUrl` | Offer MS |
| Retail API | `OfferMs:HostKey` | Offer MS — Azure Function host key |
| Retail API | `OrderMs:BaseUrl` | Order MS |
| Retail API | `UserMs:JwtSecret` | Staff JWT signing secret (Base64-encoded 256-bit key) — used to validate staff tokens on admin endpoints |
| Retail API | `UserMs:JwtIssuer` | Staff JWT issuer (default: `apex-air-user`) |
| Retail API | `UserMs:JwtAudience` | Staff JWT audience (default: `apex-air-reservation`) |
| Retail API | `PaymentMs:BaseUrl` | Payment MS |
| Retail API | `DeliveryMs:BaseUrl` | Delivery MS |
| Retail API | `AncillaryMs:BaseUrl` | Ancillary MS |
| Timatic Simulator | `Timatic:ApiToken` | Expected Bearer token — SHA-256 hash compared against each incoming `Authorization: Bearer` value |
