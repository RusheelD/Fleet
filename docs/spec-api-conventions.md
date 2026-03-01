# Fleet — API Conventions

## General Conventions

These conventions apply to all API controllers in `Fleet.Server/Controllers/`.

### URL Structure

- All API routes are prefixed with `/api/`
- Resource names are **plural** and **kebab-case** where multi-word (e.g., `/api/work-items`)
- Nested resources use the parent's ID: `/api/projects/{projectId}/work-items`
- Actions that don't fit CRUD use verbs: `POST /api/projects/{id}/agents/launch`

### HTTP Methods

| Method | Usage |
| --- | --- |
| `GET` | Read resource(s) — never mutates state |
| `POST` | Create a new resource or trigger an action |
| `PUT` | Full replace of a resource |
| `PATCH` | Partial update of a resource |
| `DELETE` | Remove a resource |

### Request / Response Format

- All request and response bodies are **JSON** (`application/json`)
- Use C# record types for DTOs (request and response models) in a `Models/` folder
- Property naming: **camelCase** in JSON (ASP.NET default with `System.Text.Json`)

### Pagination

For list endpoints that may return many items:

```json
GET /api/projects/{id}/work-items?page=1&pageSize=25

{
  "items": [ ... ],
  "page": 1,
  "pageSize": 25,
  "totalCount": 142
}
```

- Default `pageSize`: 25
- Max `pageSize`: 100
- Wrap paginated responses in a generic `PagedResult<T>` type

### Error Responses

Use the built-in **Problem Details** format (RFC 9457), already enabled via `builder.Services.AddProblemDetails()`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "Project with ID '123' was not found."
}
```

- 400 — Validation errors (automatic via `[ApiController]`)
- 401 — Missing or invalid Bearer token
- 403 — Authenticated but not authorized
- 404 — Resource not found
- 409 — Conflict (e.g., duplicate project name)
- 500 — Unhandled server error (generic message, details logged)

### Authentication

- All `/api/*` endpoints require a valid Bearer token **except**:
  - `GET /health`, `GET /alive` (health checks)
- Use `[Authorize]` attribute on controllers (or globally via policy)
- Use `[AllowAnonymous]` for any public endpoints

### Caching

- Apply `[OutputCache]` on GET endpoints that return relatively static data
- Cache durations vary by resource (e.g., project list: 5s, work items: no cache during active agent execution)

### Versioning

- No API versioning in V1 — single version
- If needed later, use URL-based versioning (`/api/v2/...`)
