# Security Principles
**Apex Air — Reservation Platform**

---

## Transport Security

- **All communication must use TLS 1.2 or higher, with TLS 1.3 preferred.**
  Unencrypted HTTP must not be accepted on any endpoint in any environment — development, staging, or production. Downgrade attacks must be actively rejected at the API Gateway and orchestration layer. Internal service-to-service communication within the Azure private network must also be encrypted; trust of the internal network perimeter alone is insufficient.

- **HTTP Strict Transport Security (HSTS) must be enforced on all public-facing endpoints.**
  HSTS headers with a minimum `max-age` of one year must be set, including `includeSubDomains` and `preload` directives where applicable, to prevent protocol downgrade attacks via cached responses.

- **Certificate management must be automated.**
  TLS certificates must be renewed automatically (e.g. via Azure-managed certificates or Let's Encrypt automation) to prevent expiry-related outages. Certificate expiry must be monitored with alerts firing at 30 and 14 days before expiry.

---

## Authentication and Authorisation

- **Customer-facing APIs must use OAuth 2.0 / OpenID Connect with short-lived JWT access tokens.**
  Access tokens should have a maximum TTL of 15 minutes. Refresh tokens are persisted and rotated on each use (single-use semantics), invalidating the previous token immediately to limit the blast radius of a stolen token.

- **Guest booking flows must validate booking reference, given name, and surname together as a compound credential.**
  No single factor alone must be sufficient to retrieve order data in an unauthenticated flow. All three values must be validated server-side on every request; they must not be cached in a session after initial validation without re-validation on sensitive operations.

- **Internal service-to-service calls must use managed identities or scoped API keys.**
  No microservice or orchestration API should be reachable without authentication. Static credentials must not be used for service-to-service authentication where managed identities are available. Scoped keys must be rotated on a defined schedule.

- **Role-Based Access Control (RBAC) must be applied to all internal-facing tooling and channel applications.**
  Airport App, Contact Centre App, and Accounting System App must enforce least-privilege access. Staff must only be granted the permissions required for their role. Privileged access (e.g. manual order modification, refund issuance) must require explicit role assignment and must be audited.

- **Multi-factor authentication (MFA) must be enforced for all staff-facing applications.**
  This includes Contact Centre, Airport App, Accounting System App, and any internal administration tooling. Customer-facing MFA is recommended at login for high-value operations (e.g. payment method changes, name corrections).

- **Token validation must occur without a database round-trip on every API request.**
  The Retail API and Loyalty API must validate JWT access tokens using the Identity microservice's public signing key via asymmetric cryptography (RS256 or ES256). This avoids the Identity DB becoming a bottleneck on every authenticated request.

---

## Encryption at Rest

- **All databases must have encryption at rest enabled.**
  Platform-managed keys are the minimum baseline. Customer-managed keys (CMK) via Azure Key Vault are required for any data store holding PII or payment data — this includes all SQL schemas within the shared database instance, the Delivery DB, and any blob storage used for boarding card generation or document storage.

- **Sensitive fields must not be stored in plain text even within an encrypted data store.**
  Passwords must be hashed with Argon2id (bcrypt as fallback). Card numbers must never be stored — only the last four digits and card type are permissible. Passport numbers and other travel document data should be treated as sensitive at rest and in transit.

- **Encryption keys must be stored in Azure Key Vault and rotated on a defined schedule.**
  Key rotation must not require application redeployment. Services must support seamless key rotation with no downtime using key versioning.

---

## PII Handling

- **Passenger personal data must be treated as PII and handled in accordance with UK GDPR and applicable destination-country regulations.**
  This includes names, dates of birth, passport and travel document numbers, contact details, and loyalty account information. Processing must have a documented lawful basis and must not extend beyond what is necessary for the stated purpose.

- **PII must never appear in application logs, telemetry, or error messages in plain text.**
  Log entries referencing passengers must use anonymised identifiers such as `PassengerId` or `BookingReference`. This applies to structured logging, distributed tracing, and any third-party observability platform. Log pipelines must be configured to scrub or mask PII before ingestion.

- **Data retention policies must be defined per domain and enforced via automated purge jobs.**
  Retention periods must reflect both regulatory requirements (e.g. airline regulatory obligations of up to 7 years for financial and manifest records) and data minimisation principles. Personal data must be purged or anonymised as soon as the retention period lapses.

- **Data subject rights (access, erasure, portability) must be supportable across all domains.**
  Designs must account for the ability to locate, export, and delete personal data on request, including data held in JSON columns and event bus payloads. Erasure requests must propagate to all domains that have consumed and stored that data.

- **Cross-border data transfers must be assessed and documented.**
  Routes to North America, Asia-Pacific, and the Caribbean mean passenger APIS data crosses jurisdictions. Transfers must comply with UK GDPR's international transfer requirements (e.g. adequacy decisions, standard contractual clauses).

---

## Payment Data

- **The platform must achieve and maintain PCI DSS compliance for all payment flows.**
  Scope must be minimised by ensuring that full card numbers, CVV codes, and raw processor tokens never pass through or are stored by any system other than the Payment microservice and its downstream payment processor. Compliance must be assessed annually by a Qualified Security Assessor (QSA).

- **Only the last four digits of a card number and the card type may be retained after a transaction.**
  These values may be stored in the Payment DB and in `OrderData` solely for customer recognition and dispute resolution purposes. Full PANs must not be logged, stored in memory beyond the transaction, or passed between services.

- **Payment processor tokens must be held in memory only and discarded after settlement.**
  Processor-issued tokens used during authorisation must not be written to disk, logs, or any persistent store. If a re-authorisation is required, a new token must be obtained from the processor.

---

## Input Validation and API Hardening

- **All API inputs must be validated at the orchestration layer before being forwarded to microservices.**
  Validation includes type checking, length constraints, format validation (e.g. IATA codes, date formats, currency codes), and rejection of unexpected fields (strict schema validation to prevent mass assignment). Microservices must also validate inputs independently, as they may be called by multiple orchestration paths.

- **All database queries must use parameterised queries or ORM-generated queries.**
  Dynamic SQL construction from user input is prohibited. This applies equally to JSON path expressions used against `NVARCHAR(MAX)` columns.

- **API rate limiting must be applied to all public-facing endpoints.**
  Limits must be defined per endpoint based on expected usage patterns, with burst allowances. Rate limit responses must use HTTP 429 with a `Retry-After` header. NDC endpoints must be rate-limited per partner key.

- **CORS policies must be explicitly configured and restricted to known channel origins.**
  Wildcard origins (`*`) are prohibited on any endpoint that handles authenticated requests or sensitive data. Allowed origins must be maintained as a configuration list and reviewed regularly.

- **Security headers must be applied to all HTTP responses.**
  This includes `Content-Security-Policy`, `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, and `Permissions-Policy` as appropriate to each channel type.

---

## Secrets Management

- **Connection strings, API keys, and credentials must not be stored in source code, configuration files, or environment variables in plain text.**
  All secrets must be stored in Azure Key Vault and accessed at runtime via managed identity. This applies to all environments, including development and CI/CD pipelines.

- **Secret rotation must be supported without requiring application redeployment.**
  Services must retrieve secrets at startup or refresh them on a defined schedule. Azure Key Vault references in App Settings should be used to enable automatic secret refresh.

- **Secrets must never be echoed in API responses, logs, or error messages.**
  This includes partial values. Any accidental secret exposure must be treated as a security incident requiring immediate rotation.

---

## Audit Logging

- **All state-changing operations must produce a structured audit log entry.**
  This includes order creation, payment authorisation and settlement, check-in, manifest writes, PAX updates, cancellations, and any manual agent actions. Each entry must record: actor identity, timestamp (UTC), operation type, affected entity (e.g. `BookingReference`, `OrderId`), and a summary of the change.

- **Audit logs must be immutable and stored separately from application logs.**
  Audit logs must not be modifiable by application-layer services. Write-once storage (e.g. Azure Immutable Blob Storage) must be used for audit log archival. Application logs and audit logs must have separate retention policies and access controls.

- **Audit logs must be retained for a minimum of 7 years.**
  This reflects airline regulatory requirements for financial and operational records. Automated archival must ensure logs are retained and retrievable throughout this period without manual intervention.

- **Failed authentication attempts and privilege escalation events must be logged and alerted.**
  Account lockout events, repeated failed logins, and any attempt to access data outside a user's authorised scope must generate alerts to the security operations function.

---

## Vulnerability and Dependency Management

- **All service dependencies must be kept up to date and scanned for known vulnerabilities as part of the CI/CD pipeline.**
  This applies to NuGet packages, npm packages, and base container/runtime images. Critical and high-severity CVEs must block deployment. Dependency scanning must run on every pull request and on a scheduled basis against deployed versions.

- **Penetration testing must be conducted at least annually and after significant architectural changes.**
  Testing must cover all public-facing API endpoints, authentication flows, payment flows, and internal service boundaries. Findings must be tracked to remediation with defined SLAs by severity.

- **A Software Bill of Materials (SBOM) must be maintained for all deployed services.**
  SBOMs enable rapid identification of affected components when new CVEs are disclosed. Generation must be automated as part of the build pipeline.

- **Security threat modelling must be performed for new capabilities before development begins.**
  STRIDE or equivalent methodology must be applied to identify and document threats. Mitigations must be incorporated into the design before implementation.

---

## Incident Response

- **A documented security incident response plan must exist and be tested at least annually.**
  The plan must define escalation paths, communication responsibilities, containment procedures, and post-incident review obligations. Regulatory notification timelines (e.g. 72 hours under UK GDPR for personal data breaches) must be explicitly addressed.

- **All services must emit sufficient telemetry to support forensic investigation following a security incident.**
  Logs must be centralised, tamper-evident, and searchable. Correlation identifiers (e.g. `BookingReference`, `BasketId`, `PaymentReference`) must be present in all log entries to enable end-to-end tracing of a transaction across services.
