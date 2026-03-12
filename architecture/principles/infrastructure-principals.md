# Infrastructure Principles
**Apex Air — Reservation Platform**

---

## Cloud Platform

Microsoft Azure is the target cloud platform; all infrastructure must be PaaS-first and provisioned as code.

- All compute, storage, networking, and platform services sourced from Azure unless a specific capability is unavailable and a compelling alternative is approved.
- PaaS and serverless offerings preferred over IaaS: Azure Functions, Azure SQL, Azure Service Bus, Azure Static Web Apps; custom VMs only when no suitable PaaS alternative exists.
- All Azure resources defined as Infrastructure as Code (Bicep or Terraform), version-controlled, peer-reviewed, and applied via CI/CD; manual portal provisioning prohibited in staging and production.

---

## Compute

Microservices run as Azure Functions (isolated worker model, C#), grouped by domain.

- All microservices use the isolated worker model for cleaner host separation, better DI support, and a predictable middleware pipeline; legacy in-process functions must be migrated.
- One Function App per domain (Offer, Order, Payment, Delivery, Customer, Identity, Accounting, Seat); domain-aligned deployment units prevent one domain's deployment impacting another.
- Premium plans (pre-warmed) for latency-sensitive services in the synchronous booking path; Consumption plans acceptable for background jobs (basket expiry, offer purge).
- Front-end applications (Web, App, Contact Centre, Airport) built in Angular and hosted on Azure Static Web Apps for CDN delivery, integrated CI/CD, and managed SSL.

---

## Networking and Topology

All services are hosted within an Azure VNet and must not be directly reachable from the public internet.

- Public access routed through Azure API Management (APIM), which handles TLS termination, rate limiting, authentication, logging, and routing.
- APIM is the API Gateway for all public-facing and partner-facing endpoints; NDC XML-to-JSON transformation handled at the APIM layer where possible.
- Private endpoints used for all PaaS service connectivity (Azure SQL, Service Bus, Key Vault, Storage); public access to these resources disabled.
- Network Security Groups (NSGs) applied to all subnets with least-privilege rules; version-controlled as IaC and reviewed as part of security assurance.

---

## Managed Identity and Access

All Azure resources authenticate via Managed Identities; no embedded credentials permitted.

- System-assigned Managed Identities used for Function Apps authenticating to Azure SQL, Key Vault, and Service Bus; no service principal credentials or connection strings with embedded secrets.
- Role assignments follow least-privilege and are defined in IaC (e.g. `Key Vault Secrets User` not `Key Vault Contributor`; `Service Bus Data Sender` on specific topics only).
- Azure Key Vault stores all secrets, certificates, and encryption keys; Key Vault diagnostic logs enabled and retained.

---

## Messaging and Event Bus

Azure Service Bus is used for all asynchronous service communication, with durable delivery and dead-letter monitoring.

- Service Bus provides durable, ordered, at-least-once delivery; used for `OrderConfirmed` and `OrderChanged` events driving Accounting and Customer point accrual.
- Topics and subscriptions for events with multiple consumers (e.g. `OrderConfirmed` consumed by both Accounting and Customer); dedicated subscriptions per consumer.
- Dead-letter queues monitored with alerts on queue depth threshold; a runbook must exist for investigation and reprocessing.
- Consumers must implement idempotency (e.g. tracking processed message IDs or using database unique constraints) to handle at-least-once delivery.
- Event schemas versioned; breaking changes require a version increment with consumers updated to handle both versions before the publisher stops emitting the old version.

---

## Database Infrastructure

A single Azure SQL instance with logical schema separation is the starting point, targeting zone-redundant replication in production.

- Shared instance acceptable during initial build; architecture enforces no cross-schema joins in application code so physical separation can be achieved via connection string change only.
- Zone-redundant replication enabled in production; geo-redundant backups enabled for cross-region disaster recovery.
- Automated backup retention of at least 35 days (point-in-time restore); long-term retention (LTR) policies meet the 7-year regulatory requirement for audit data.
- Database firewall rules restrict access to the VNet only; public endpoint access disabled; admin access via Azure Bastion or VNet jump host, not public IP.

---

## Environments

A minimum of three environments (Development, Staging, Production) with promotion gates between each.

- Development for feature work and integration testing; Staging production-equivalent for pre-release validation, load testing, and security testing; Production for live traffic.
- Staging must match Production configuration and topology; documented differences (e.g. reduced SKU sizes) must be accounted for in test result interpretation; no shared credentials across environments.
- Environment-specific configuration externalised via Azure App Configuration or Key Vault references; build artefacts must be environment-agnostic.
- Production deployments require a passing Staging build, automated test gate, and manual approval by an authorised release manager; emergency change procedures documented and subject to post-incident review.

---

## CI/CD

All services have a CI/CD pipeline covering build, test, security scanning, and deployment, defined and versioned as code.

- Pipelines defined as code (GitHub Actions or Azure DevOps YAML), version-controlled and peer-reviewed alongside application code.
- Unit test failures, integration test failures, critical/high CVE findings, and SAST alerts all block promotion; no build promoted without passing all gates.
- Zero-downtime deployments via slot swaps (staging slot → production); database migrations applied before slot swap and must be backwards-compatible with the currently running version.
- Rollback achievable within 5 minutes via slot swap-back; procedures documented, tested in staging, and executable by on-call engineers without the original deploying engineer.

---

## Monitoring and Alerting

Azure Monitor and Application Insights are the centralised observability platform for all services.

- All Function Apps instrumented with Application Insights (workspace-based mode); traces, dependencies, exceptions, custom events, and metrics routed to a shared instance per environment.
- Alerts defined for all critical business operations and infrastructure thresholds: booking confirmation failure rate, payment failure rate, e-ticket issuance failure rate, DB connection pool exhaustion, Service Bus dead-letter queue depth, Function App error rate, and SLA latency breaches.
- Every production alert must have an on-call runbook describing the alert, likely causes, investigation steps, and remediation; updated after every relevant incident.
- Availability SLAs defined and dashboarded; booking confirmation path targets ≥ 99.9% availability; individual services have defined targets aligned to their position in the critical path.

---

## Disaster Recovery

RTO and RPO targets must be defined, infrastructure configured accordingly, and DR procedures tested annually.

- Booking confirmation path and payment services have the tightest RTO/RPO requirements; targets agreed with the business and reflected in replication, backup frequency, and geo-redundancy configuration.
- DR procedures documented and tested at least annually, exercising full failover including database failover, service reconnection, and booking path validation.
- Geo-redundant deployment is the target state using Azure paired regions; active-passive is the acceptable starting posture with active-active geo-distribution the target for the booking confirmation path.
