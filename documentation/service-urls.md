# Service URLs — live environments

All services are deployed to Azure App Service (Azure Functions v4, .NET 8 isolated worker process) and hosted in the `uksouth-01` or `ukwest-01` Azure regions.

---

## Orchestration APIs

| Service | Base URL | Region |
|---------|----------|--------|
| Retail API | `https://apexair-retail-api-c7cbf3a2adfhfacu.ukwest-01.azurewebsites.net` | UK West |
| Loyalty API | `https://reservation-system-db-api-loyalty-gufra2fxfdd2eka6.uksouth-01.azurewebsites.net` | UK South |
| Operations API | `https://reservation-system-db-api-operations-gzfhekfvawaubkbs.uksouth-01.azurewebsites.net` | UK South |
| Disruption API | `https://apexair-disruption-api-f0feg6d5dgjijdfx.ukwest-01.azurewebsites.net` | UK West |
| Admin API | `https://reservation-system-db-api-admin-ageucwaad3axbxhm.uksouth-01.azurewebsites.net` | UK South |

---

## Microservices

| Service | Base URL | Region |
|---------|----------|--------|
| Offer MS | `https://reservation-system-db-microservice-offer-dnfdbebdezemaghp.uksouth-01.azurewebsites.net` | UK South |
| Order MS | `https://apexair-order-ms-b2bch8f7fiklkfhz.ukwest-01.azurewebsites.net` | UK West |
| Payment MS | `https://apexair-payment-ms-c3cdi9g8gjlmlgia.ukwest-01.azurewebsites.net` | UK West |
| Delivery MS | `https://apexair-delivery-ms-d4dej0h9hkmnmhjb.ukwest-01.azurewebsites.net` | UK West |
| Customer MS | `https://reservation-system-db-microservice-customer-axdydza6brbkc0ck.uksouth-01.azurewebsites.net` | UK South |
| Seat MS | `https://apexair-seat-ms-f6fgl2j1jmopojld.ukwest-01.azurewebsites.net` | UK West |
| Bag MS | `https://apexair-bag-ms-g7ghm3k2knpqpkme.ukwest-01.azurewebsites.net` | UK West |
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

Each orchestration API reads downstream base URLs from Azure App Settings. The keys follow the pattern `<ServiceName>:BaseUrl`.

| API | Config key | Target service |
|-----|-----------|----------------|
| Operations API | `ScheduleMs:BaseUrl` | Schedule MS |
| Operations API | `OfferMs:BaseUrl` | Offer MS |
| Admin API | `UserMs:BaseUrl` | User MS |
| Admin API | `UserMs:HostKey` | User MS — Azure Function host key |
| Loyalty API | `IdentityMs:BaseUrl` | Identity MS |
| Loyalty API | `CustomerMs:BaseUrl` | Customer MS |
| Retail API | `OfferMs:BaseUrl` | Offer MS |
| Retail API | `OrderMs:BaseUrl` | Order MS |
| Retail API | `PaymentMs:BaseUrl` | Payment MS |
| Retail API | `DeliveryMs:BaseUrl` | Delivery MS |
| Retail API | `SeatMs:BaseUrl` | Seat MS |
| Retail API | `BagMs:BaseUrl` | Bag MS |
