# Infrastructure Principles
**Apex Air — Reservation Platform**

---

## Cloud Platform

- **Microsoft Azure is the target cloud platform for all infrastructure.**
  All compute, storage, networking, and platform services must be sourced from Azure unless a specific capability is unavailable or a compelling third-party case is made and approved. Consistency of tooling, identity, and governance across the platform depends on single-platform discipline.

- **Platform-as-a-Service (PaaS) and serverless offerings must be preferred over Infrastructure-as-a-Service (IaaS).**
  Azure Functions (serverless compute), Azure SQL (managed database), Azure Service Bus (managed messaging), and Azure Static Web Apps (managed hosting) reduce operational overhead and shift patching and availability responsibilities to Microsoft. Custom virtual machines must only be used when no suitable PaaS alternative exists.

- **Infrastructure must be provisioned and managed as code.**
  All Azure resources must be defined using Infrastructure as Code (IaC) — Bicep or Terraform are the preferred tools. Manual portal-based provisioning is prohibited in staging and production environments. IaC definitions must be version-controlled, peer-reviewed, and applied via a CI/CD pipeline.

---

## Compute

- **Microservices must be implemented as Azure Functions using the isolated worker model in C#.**
  The isolated worker model is the current Microsoft-recommended pattern and provides cleaner separation from the Functions host, better dependency injection support, and a more predictable middleware pipeline. All new functions must target this model; legacy in-process functions must be migrated.

- **Function apps must be grouped by domain, not by technical concern.**
  Each microservice domain (Offer, Order, Payment, Delivery, Customer, Identity, Accounting, Seat) must have its own Function App. This aligns deployment units with domain boundaries and prevents a deployment of one domain from impacting the availability of another.

- **Consumption plans must be used for variable or low-frequency workloads; Premium plans for latency-sensitive or always-warm services.**
  The booking confirmation path requires consistently low latency. Azure Functions on a Premium plan (with pre-warmed instances) must be used for the Retail API orchestration layer and any microservice in the synchronous booking path. Background jobs (basket expiry, offer purge) may run on Consumption plans.

- **Front-end applications must be hosted on Azure Static Web Apps.**
  Web, App (progressive web app), Contact Centre App, and Airport App front ends are built in Angular and must be deployed to Azure Static Web Apps. This provides CDN-backed global delivery, integrated CI/CD, and managed SSL — with no server-side compute required for static assets.

---

## Networking and Topology

- **All microservices and orchestration APIs must be hosted within an Azure Virtual Network (VNet) and must not be directly accessible from the public internet.**
  Public access must be routed through an API Gateway (e.g. Azure API Management) which handles TLS termination, rate limiting, authentication enforcement, and routing. Internal service-to-service communication must traverse the VNet using private endpoints.

- **Azure API Management (APIM) must be used as the API Gateway for all public-facing and partner-facing endpoints.**
  APIM enforces consistent policies (authentication, rate limiting, logging, CORS, request transformation) across all channels. NDC XML-to-JSON transformation for partner integrations must be handled at the APIM layer where possible, keeping internal services JSON-native.

- **Private endpoints must be used for all PaaS service connectivity (Azure SQL, Azure Service Bus, Azure Key Vault, Azure Storage).**
  Traffic between services and PaaS resources must remain on the Microsoft backbone and must not traverse the public internet. Public access to these resources must be disabled.

- **Network Security Groups (NSGs) must be applied to all subnets with least-privilege inbound and outbound rules.**
  Each service subnet must only allow the traffic flows required for its operation. NSG rules must be version-controlled as IaC and reviewed as part of the security assurance process.

---

## Managed Identity and Access

- **All Azure resources must use Managed Identities for service-to-service authentication.**
  No service principal credentials or connection strings with embedded secrets may be used where a Managed Identity is supported. System-assigned Managed Identities must be used for Function Apps authenticating to Azure SQL, Azure Key Vault, and Azure Service Bus.

- **Role assignments must follow least-privilege.**
  Managed Identities must be granted only the permissions required for the service's operation (e.g. `Key Vault Secrets User`, not `Key Vault Contributor`; `Azure Service Bus Data Sender` on specific queues/topics, not subscription-wide). Role assignments must be defined in IaC and reviewed regularly.

- **Azure Key Vault must be used for all secrets, certificates, and encryption keys.**
  Key Vault access must be mediated by Managed Identity. Secrets must never be embedded in application settings, environment variables, or deployment artefacts. Key Vault diagnostic logs must be enabled and retained.

---

## Messaging and Event Bus

- **Azure Service Bus must be used as the event bus for all asynchronous service communication.**
  Service Bus provides durable, ordered, at-least-once message delivery with built-in dead-letter queues, message lock renewal, and session support. It is the appropriate choice for the `OrderConfirmed` and `OrderChanged` events that drive Accounting and Customer point accrual.

- **Topics and subscriptions must be used for events with multiple consumers.**
  The `OrderConfirmed` event is consumed by both Accounting and Customer microservices; a Service Bus topic with two subscriptions (one per consumer) must be used rather than a shared queue. This decouples consumers from each other and from the publisher.

- **Dead-letter queues must be monitored and alerted.**
  Messages that cannot be processed after the configured maximum delivery count must be moved to the dead-letter queue. An alert must fire when dead-letter queue depth exceeds a defined threshold. A runbook must exist for dead-letter queue investigation and reprocessing.

- **Message idempotency must be enforced by consumers.**
  At-least-once delivery means consumers may receive the same message more than once. Consumers must implement idempotency (e.g. by tracking processed message IDs or using database unique constraints) to ensure duplicate delivery does not result in duplicate accounting entries or duplicate points accrual.

- **Message schemas must be versioned and documented.**
  Event payloads must include a schema version field. Breaking changes to event schemas must be managed via a version increment, with consumers updated to handle both the old and new versions before the publisher stops emitting the old version.

---

## Database Infrastructure

- **A single Azure SQL instance with logical schema separation is used for this project, with physical separation as the target state for production scaling.**
  The shared instance is an acceptable starting point to reduce operational complexity during initial build. The architecture must treat each schema as if it were a separate database — no cross-schema joins in application code — so that physical separation can be achieved by changing connection strings alone.

- **Azure SQL must be configured with zone-redundant replication in production.**
  Zone redundancy ensures the database remains available in the event of an Azure availability zone failure. Geo-redundant backups must also be enabled to support disaster recovery across regions.

- **Automated backups must be configured with a retention period appropriate to the data's regulatory requirements.**
  Financial and manifest data must have a backup retention period of at least 35 days (Azure SQL maximum for point-in-time restore) with geo-redundant backup storage. Long-term retention (LTR) policies must be used to meet the 7-year regulatory retention requirement for audit data.

- **Database firewall rules must restrict access to the VNet only.**
  Public endpoint access to Azure SQL must be disabled. All database connectivity must use private endpoints within the VNet. Database administrator access must be via Azure Bastion or a jump host within the VNet, not via public IP.

---

## Environments

- **A minimum of three environments must exist: Development, Staging, and Production.**
  Development is used for active feature development and integration testing. Staging is a production-equivalent environment used for pre-release validation, load testing, and security testing. Production serves live traffic. Promotion between environments must follow a defined release process.

- **Staging must be as close to Production as possible in configuration and topology.**
  Differences between Staging and Production (e.g. reduced SKU sizes) must be documented and accounted for in test result interpretation. Secrets, connection strings, and third-party integrations must be environment-specific; no shared credentials between environments.

- **Environment-specific configuration must be externalised and must not be baked into deployment artefacts.**
  Azure App Configuration or environment-specific Key Vault references must be used to supply environment-specific values at runtime. Build artefacts must be environment-agnostic.

- **Production must have change approval gates enforced in the CI/CD pipeline.**
  Deployment to Production must require a passing Staging deployment, automated test gate, and a manual approval step by an authorised release manager. Emergency change procedures must be documented and require post-incident review.

---

## CI/CD

- **All services must have a CI/CD pipeline covering build, test, security scan, and deployment.**
  Pipelines must be defined as code (e.g. GitHub Actions, Azure DevOps YAML pipelines) and version-controlled alongside the service code. Pipeline definitions must be peer-reviewed in the same way as application code.

- **Automated test gates must block deployment on failure.**
  Unit test failures, integration test failures, critical/high CVE findings, and SAST (static application security testing) alerts must all block promotion to the next environment. No pipeline should promote a build that has not passed all gates.

- **Deployments must be zero-downtime by default.**
  Azure Functions support slot-based deployments (staging slot → production slot swap). Slot swaps must be used for all production deployments to avoid cold-start downtime. Database migrations must be applied before the new function version is swapped in and must be backwards-compatible with the currently running version.

- **Rollback must be achievable within 5 minutes for any production deployment.**
  Slot swap deployments support a swap-back as a rollback mechanism. Rollback procedures must be documented, tested in staging, and executable by on-call engineers without requiring the original deploying engineer.

---

## Monitoring and Alerting

- **Azure Monitor and Application Insights must be the centralised observability platform.**
  All Function Apps must be instrumented with Application Insights. Telemetry — traces, dependencies, exceptions, custom events, and metrics — must be routed to a shared Application Insights instance per environment (with workspace-based mode enabled for Log Analytics integration).

- **Alerts must be defined for all critical business operations and infrastructure thresholds.**
  This includes: booking confirmation failure rate, payment failure rate, e-ticket issuance failure rate, database connection pool exhaustion, Service Bus dead-letter queue depth, Function App error rate, and response latency breaching SLA thresholds.

- **On-call runbooks must exist for every production alert.**
  An alert without a runbook is a gap in operational readiness. Runbooks must describe the alert, likely causes, investigation steps, and remediation actions. Runbooks must be reviewed and updated after every relevant incident.

- **Availability SLAs must be defined and dashboarded.**
  The booking confirmation path must target ≥ 99.9% availability. Individual microservices and orchestration APIs must have defined availability targets aligned to their position in the critical path. SLA tracking must be automated and visible to engineering and operations leadership.

---

## Disaster Recovery

- **A Recovery Time Objective (RTO) and Recovery Point Objective (RPO) must be defined for all critical services.**
  The booking confirmation path and payment services are the most time-sensitive. RTO and RPO targets must be agreed with the business and reflected in infrastructure configuration (replication, backup frequency, geo-redundancy).

- **Disaster recovery procedures must be documented and tested at least annually.**
  DR tests must exercise the full failover sequence, including database failover, service reconnection, and validation that the booking path is operational. Test results must be documented and acted upon.

- **Geo-redundant deployment must be the target state for production services.**
  Azure paired regions must be used for failover. Active-passive is an acceptable starting posture; active-active geo-distribution is the target for the booking confirmation path to meet airline availability expectations.
