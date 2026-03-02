# Fleet Documentation

This folder contains specification and design documents for the Fleet project.

## Structure

Place spec documents in this directory using the following naming convention:

- `spec-<feature-name>.md` — Feature specification documents
- `design-<topic>.md` — Design and architecture documents
- `adr-<number>-<title>.md` — Architecture Decision Records

## Index

1. [Product Vision & Overview](spec-product-vision.md) — Mission, target users, core workflow, agent model, GitHub integration scope
2. [Users, Auth & Plans](spec-users-auth-plans.md) — OAuth providers, account model, pricing tiers, data storage
3. [Projects, Work Items & AI Chat](spec-projects-workitems-chat.md) — Project↔repo mapping, chat modes, work item structure, PR output
4. [Agent Execution & Architecture](spec-agent-execution.md) — Execution infrastructure, LLM providers, monitoring & control
5. [Agent Roles & Execution Flow](spec-agent-roles-flow.md) — Role definitions, phase sequencing, agent communication
6. [UI Structure & Frontend Architecture](spec-ui-frontend.md) — Navigation, routing, state management, real-time transport
7. [Backend API & Data Model](spec-backend-api.md) — Controllers → Services → Repositories architecture, domain folders, EF Core + Postgres, entity sketch
8. [GitHub Integration & Safety](spec-github-integration.md) — OAuth/GitHub App, branch conventions, safety guardrails, MVP scope
9. [Infrastructure, Testing & Open Items](spec-infrastructure.md) — Azure hosting, MSTest + Vitest, telemetry, future considerations
10. [Authentication & Security](spec-auth-security.md) — Azure AD B2C, MSAL, GitHub token flow, Stripe payments
11. [API Conventions](spec-api-conventions.md) — HTTP methods, pagination, error format, caching, auth requirements
