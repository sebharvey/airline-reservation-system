# Security Principles
**Apex Air — Reservation Platform**

---

## Transport Security

All communication must be encrypted in transit with no exceptions across any environment.

- TLS 1.2 minimum (TLS 1.3 preferred) on all endpoints including internal service-to-service; unencrypted HTTP rejected at the API Gateway — internal network trust alone is insufficient.
- HSTS enforced on all public-facing endpoints with `max-age` of at least one year, including `includeSubDomains` and `preload` directives.
- TLS certificates renewed automatically (e.g. Azure-managed or Let's Encrypt); expiry monitored with alerts at 30 and 14 days before expiry.

---

## Authentication and Authorisation

Authentication must use short-lived tokens with least-privilege access enforced at every layer.

- Customer-facing APIs use OAuth 2.0 / OIDC with JWTs (max 15-minute TTL); refresh tokens single-use and rotated on each use.
- Guest booking flows require booking reference, given name, and surname validated together server-side on every request — no single factor is sufficient alone.
- **Orchestration API → Microservice calls are authenticated using Azure Function Host Keys.** Host keys are generated automatically when each Azure Function microservice is first deployed. The orchestration APIs (Retail API, Loyalty API, Airport API, Finance API, Disruption API, Operations API), acting as microservice consumers, pass the host key on every inbound request via the `x-functions-key` HTTP header. Microservices reject any request that does not carry a valid host key.
- Host keys are stored in Azure Key Vault and accessed by orchestration services at runtime via managed identity — keys must never be embedded in source code, configuration files, or environment variables in plain text.
- RBAC enforced on all staff-facing apps (Airport, Contact Centre, Accounting); least-privilege access with privileged actions (manual order modification, refunds) requiring explicit role assignment and audit logging.
- MFA enforced for all staff-facing applications; recommended for customers on high-value operations (e.g. payment method changes).
- JWT validation uses the Identity MS public signing key (RS256 or ES256) — no database round-trip on every API request.

---

## Encryption at Rest

All data stores must be encrypted, with sensitive fields additionally protected at the field level.

- Encryption at rest enabled on all databases; customer-managed keys (CMK) via Azure Key Vault required for any store holding PII or payment data.
- Passwords hashed with Argon2id (bcrypt as fallback); card numbers never stored — only last four digits and card type permitted.
- Encryption keys stored in Azure Key Vault, rotated on a defined schedule; rotation must require no application redeployment, supported via key versioning.

---

## PII Handling

Passenger personal data is treated as PII in accordance with UK GDPR and all applicable destination-country regulations.

- PII includes names, dates of birth, passport/travel document numbers, contact details, and loyalty information; processing must have a documented lawful basis.
- PII must never appear in logs, telemetry, or error messages; log entries use anonymised identifiers (`PassengerId`, `BookingReference`); log pipelines configured to scrub PII before ingestion.
- Retention periods defined per domain and enforced via automated purge jobs; personal data purged or anonymised once the retention period lapses.
- Data subject rights (access, erasure, portability) must be supportable across all domains, including JSON columns and event bus payloads; erasure propagates to all domains that have stored the data.
- Cross-border APIS data transfers (North America, Asia-Pacific, Caribbean routes) must comply with UK GDPR international transfer requirements (adequacy decisions or SCCs).

---

## Payment Data

PCI DSS compliance must be maintained and card data scoped entirely to the Payment microservice.

- Full card numbers, CVVs, and raw processor tokens must never pass through or be stored by any service other than Payment MS and its processor; compliance assessed annually by a QSA.
- Only the last four digits and card type may be retained after a transaction — stored in Payment DB and `OrderData` for recognition and dispute purposes only.
- Processor-issued tokens held in memory only and discarded after settlement; never written to disk, logs, or any persistent store.

---

## Input Validation and API Hardening

All inputs must be validated and APIs hardened against common attack vectors.

- Inputs validated at the orchestration layer (type, length, format, no unexpected fields) and independently by microservices; strict schema validation prevents mass assignment.
- All database queries use parameterised or ORM-generated queries; dynamic SQL from user input is prohibited, including JSON path expressions.
- Rate limiting on all public-facing endpoints (HTTP 429 with `Retry-After`); NDC endpoints rate-limited per partner key.
- CORS policies restricted to known channel origins; wildcard `*` prohibited on any authenticated or sensitive endpoint.
- Security headers (`Content-Security-Policy`, `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`) applied to all HTTP responses.

---

## Secrets Management

All secrets must be stored in Azure Key Vault and accessed at runtime via managed identity.

- Connection strings, API keys, and credentials must not appear in source code, config files, or plain-text environment variables — applies to all environments including CI/CD.
- Secret rotation supported without application redeployment; Azure Key Vault references in App Settings used for automatic refresh.
- Secrets must never appear in API responses, logs, or error messages; any accidental exposure treated as a security incident requiring immediate rotation.

---

## Audit Logging

All state-changing operations must produce a structured, immutable audit log entry.

- Logged operations include order creation, payment authorisation/settlement, check-in, manifest writes, PAX updates, cancellations, and manual agent actions; each entry records actor, UTC timestamp, operation type, affected entity, and change summary.
- Audit logs stored separately from application logs in write-once storage (e.g. Azure Immutable Blob Storage); not modifiable by application-layer services.
- Retained for a minimum of 7 years to meet airline regulatory requirements; automated archival ensures retrieval throughout.
- Failed authentication attempts, account lockouts, and privilege escalation events logged and alerted to the security operations function.

---

## Vulnerability and Dependency Management

Dependencies must be continuously scanned and security testing conducted regularly.

- All NuGet, npm, and base image dependencies scanned for CVEs in CI/CD; critical and high-severity findings block deployment; scans run on every PR and on a schedule against deployed versions.
- Penetration testing at least annually and after significant architectural changes, covering all public APIs, auth flows, payment flows, and internal service boundaries.
- Software Bill of Materials (SBOM) generated automatically at build time for all deployed services.
- Security threat modelling (STRIDE or equivalent) required for new capabilities before development begins; mitigations incorporated into design.

---

## Incident Response

A documented incident response plan must exist and be tested regularly.

- Plan defines escalation paths, communication responsibilities, containment procedures, and post-incident review obligations; UK GDPR 72-hour breach notification timeline explicitly addressed.
- All services emit sufficient telemetry for forensic investigation; logs must be centralised, tamper-evident, and searchable; correlation identifiers (`BookingReference`, `BasketId`, `PaymentId`) present in all log entries.
