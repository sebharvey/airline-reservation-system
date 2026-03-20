# Apex Air — Airline Reservation System

**A modern airline retailing platform built with an agentic-code first approach.**

Website:
https://delightful-wave-0b9bd1903.2.azurestaticapps.net

Admin:
https://proud-rock-0cb252d03.6.azurestaticapps.net

---

## What is Apex Air?

Apex Air is a full-stack airline retailing system built on [IATA ONE Order](https://www.iata.org/en/programs/ops-infra/one-order/) and [NDC](https://www.iata.org/en/programs/airline-distribution/ndc/) standards. It covers the complete passenger journey — from flight search and offer management through to booking, ancillaries, check-in, and boarding card generation.

The platform is structured around domain-driven bounded contexts, with a clean orchestration layer sitting between the Angular web front-end and a suite of independent microservices. Each domain — Offer, Order, Payment, Delivery, Customer, Seat, Loyalty, and more — owns its data and is accessed exclusively through well-defined APIs.

## What we're aiming to achieve

The goal is to demonstrate that a production-quality, standards-compliant airline retailing system can be designed and built with AI as the primary author. Every architectural decision, API contract, domain model, and line of code is shaped through deliberate prompt engineering — with human oversight guiding direction, not writing implementation.

This is an **agentic-code first** project. The majority of the codebase is AI-generated, produced by AI coding agents working from a structured set of design documents, principles, and conventions maintained in this repository. The human role is architect and reviewer; the agent's role is implementer.

## Key characteristics

- **Standards-based** — IATA ONE Order and NDC at the core, not bolted on
- **Domain-driven** — twelve bounded contexts with clear ownership and no cross-domain shortcuts
- **Price integrity** — stored offer snapshots lock prices at search time; no re-pricing on confirmation
- **Clean architecture** — every microservice follows the same layered pattern, scaffolded consistently by AI
- **Azure-native** — Azure Functions, Static Web Apps, and Azure SQL throughout

## The agentic approach

Rather than writing code directly, the team maintains a rich set of living documentation — domain capability models, API references, architectural principles, and integration conventions. AI agents consume this documentation as context and produce code, schemas, and further documentation that conforms to the established patterns.

The result is a codebase that is consistent, well-structured, and aligned to industry standards — built at a pace and scale that would be difficult to achieve through conventional means alone.
