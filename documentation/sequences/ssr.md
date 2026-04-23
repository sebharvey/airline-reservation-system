# SSR — sequence diagrams

Covers Special Service Request flows: retrieval of the SSR catalogue for the booking journey, and admin management of SSR catalogue entries.

---

## SSR options retrieval (booking flow)

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order MS

    Web->>RetailAPI: GET /v1/ssr/options
    Note over Web,RetailAPI: Optional filters: cabinCode,<br/>flightNumbers (comma-separated)
    RetailAPI->>OrderMS: GET /api/v1/ssr/options
    OrderMS-->>RetailAPI: SsrOptionsResponse
    RetailAPI-->>Web: GetSsrOptionsResponse
    Note over RetailAPI,Web: ssrOptions[] grouped by category<br/>(meals, assistance, equipment, etc.)
```

---

## Update basket SSRs

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order MS

    Web->>RetailAPI: PUT /v1/basket/{basketId}/ssrs
    Note over Web,RetailAPI: ssrSelections: [{passengerId,<br/>segmentId, ssrCode}]
    RetailAPI->>OrderMS: PUT /api/v1/basket/{basketId}/ssrs
    OrderMS-->>RetailAPI: 204 No Content
    RetailAPI-->>Web: 204 No Content
```

---

## Update order SSRs post-sale

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order MS

    Web->>RetailAPI: PATCH /v1/orders/{bookingRef}/ssrs
    Note over Web,RetailAPI: ssrSelections: [{passengerId,<br/>segmentId, ssrCode}]
    RetailAPI->>OrderMS: PATCH /api/v1/orders/{bookingRef}/ssrs
    OrderMS-->>RetailAPI: 204 No Content
    RetailAPI-->>Web: 204 No Content
```

---

## Admin — SSR catalogue management

The Admin API orchestrates all mutations against the SSR catalogue stored in the Order microservice. These endpoints require a valid staff JWT token.

```mermaid
sequenceDiagram
    participant Terminal as Contact Centre / Admin UI
    participant AdminAPI as Admin API
    participant OrderMS as Order MS

    Terminal->>AdminAPI: GET /v1/admin/ssr
    AdminAPI->>OrderMS: GET /api/v1/ssr/options
    OrderMS-->>AdminAPI: SsrOptionListResponse
    AdminAPI-->>Terminal: SsrOptionListResponse

    Terminal->>AdminAPI: POST /v1/admin/ssr
    Note over Terminal,AdminAPI: {ssrCode, label, category}
    AdminAPI->>OrderMS: POST /api/v1/ssr/options
    OrderMS-->>AdminAPI: Created SsrOption (ssrCatalogueId)
    AdminAPI-->>Terminal: 201 Created

    Terminal->>AdminAPI: PUT /v1/admin/ssr/{ssrCode}
    Note over Terminal,AdminAPI: {label, category}
    AdminAPI->>OrderMS: PUT /api/v1/ssr/{ssrCode}
    OrderMS-->>AdminAPI: Updated SsrOption
    AdminAPI-->>Terminal: SsrOptionResponse

    Terminal->>AdminAPI: DELETE /v1/admin/ssr/{ssrCode}
    AdminAPI->>OrderMS: DELETE /api/v1/ssr/{ssrCode}
    OrderMS-->>AdminAPI: 204 No Content
    AdminAPI-->>Terminal: 204 No Content
```
