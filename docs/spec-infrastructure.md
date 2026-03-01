# Fleet — Infrastructure, Testing & Open Items

## Hosting

**Azure** — Fleet will be hosted on Azure, leveraging the .NET Aspire deployment model.

Likely services (TBD exact configuration):

- **Azure Container Apps (ACA)** — Aspire's default container deployment target
- **Azure Database for PostgreSQL** — managed Postgres for the primary database
- **Azure Cache for Redis** — managed Redis for output caching
- **Azure Blob Storage** — for file attachments (chat uploads, spec documents)

> Exact Azure resource choices will be finalized during deployment setup.

## Testing

### Backend (Fleet.Server)

- **Framework:** MSTest (`Microsoft.VisualStudio.TestTools.UnitTesting`)
- **Test project:** `Fleet.Server.Tests` (to be created)
- Conventions: one test class per controller/service, `[TestMethod]` attributes, arrange-act-assert pattern

### Frontend (frontend/)

- **Framework:** Vitest
- Conventions: test files co-located with source (`*.test.ts` / `*.test.tsx`), or in a `__tests__/` folder
- Run: `npm run test` from `frontend/`

## CI/CD

**TBD** — CI/CD pipeline tooling has not been decided. GitHub Actions is the most likely choice given the GitHub-centric nature of the product.

## Telemetry & Analytics

- **Azure Application Insights** — for backend telemetry (the OpenTelemetry setup in `Extensions.cs` already has a commented-out Azure Monitor exporter ready to enable)
- Frontend analytics approach TBD

## Future Considerations

| Item | Status |
| --- | --- |
| **Mobile support** | Post-MVP — responsive web first, native mobile later |
| **Notification channels** (email, Slack, Discord) | Not yet scoped |
| **Internationalization (i18n)** | Not yet scoped |
| **Accessibility** | Fluent UI v9 provides baseline a11y; additional audits post-MVP |
| **CI/CD pipeline** | TBD |
