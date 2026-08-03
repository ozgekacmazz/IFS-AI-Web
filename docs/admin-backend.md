# Admin Backend Phase

## Scope and authorization

The backend exposes a single `/api/admin` route group. Every endpoint requires the existing `AdminOnly` policy, which combines JWT authentication, the `Admin` role claim and database-backed `ActiveUserRequirement`. Anonymous callers receive 401, authenticated `User` callers receive 403, and an inactive Admin cannot continue with an unexpired access token.

Implemented endpoints:

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/admin/users` | Bounded user list with page, pageSize, search, role and isActive filters |
| POST | `/api/admin/users` | Create a User or Admin with the shared validation and password hasher |
| PATCH | `/api/admin/users/{id}/status` | Activate or deactivate another user |
| PUT | `/api/admin/users/{id}/password` | Assign a validated new password and revoke refresh sessions |
| GET | `/api/admin/logs` | Paginated compact AI-operation records |
| GET | `/api/admin/prompt-info` | Read-only prompt metadata |
| GET | `/api/admin/statistics/seven-days` | Seven UTC calendar-day usage statistics |

No user deletion, existing-user role editing, email support, editable prompt endpoint, admin-action audit table or Admin full-source endpoint was added. The consuming admin frontend is documented in `admin-frontend.md`.

## User management and sessions

Public registration remains `User`-only. Admin creation explicitly allowlists `User` and `Admin`, reuses username/name/password validation and relies on the existing normalized-username unique index as the final race-safe duplicate check. Responses use dedicated DTOs and never contain password or token fields.

Status changes and password resets are transactions. Deactivation revokes every non-revoked refresh token belonging to the target. Activation never restores revoked tokens. Self-deactivation returns a safe 409. A PostgreSQL transaction-scoped advisory lock serializes admin-status mutations before counting active Admin accounts; consequently concurrent requests cannot deactivate the last active Admin.

Password reset replaces only the password hash, updates `updated_at_utc`, and revokes all refresh sessions. Old passwords and refresh cookies stop working. Already-issued access JWTs remain valid until their normal 15-minute expiry because the existing schema has no password/security-stamp claim. They still fail immediately if the account is inactive. No migration was introduced solely to change this documented limitation.

## Compact AI-operation privacy

The log API performs filtering, count, deterministic ordering, pagination and a bounded 512-character content projection in PostgreSQL. Application code normalizes whitespace and produces a maximum 160-text-element preview. A terminal text element is deliberately withheld and an ellipsis is always added, so even a short retained value is not returned in full.

Failed records return neither input nor summary preview and use this fixed explanation:

> Bu işlem başarısız olduğu için kaynak metin güvenlik ve mahremiyet amacıyla saklanmadı.

Expired records may remain visible as operational metadata, but both previews are omitted. The API never exposes full source/summary content, provider bodies, raw failures, exceptions, stack traces, password/token material or secrets.

## Prompt information

`GET /api/admin/prompt-info` obtains the version from `SummarizationPromptBuilder.PromptVersion`, reports its safe purpose and supported languages, and returns `editable: false`. It does not expose the internal system instruction. No prompt write endpoint exists.

## Seven-day statistics

The interval is `[UTC today 00:00 minus 6 days, UTC tomorrow 00:00)`. All seven dates are returned, including zero buckets. Inclusion uses `created_at_utc`, not expiry. PostgreSQL computes daily totals, success/failure counts, Turkish/English counts, arithmetic mean duration, per-day distinct operation-user counts, a separate distinct-user count across the full interval, and provider/model totals. Failed records participate in provider/model totals. Success rate is `succeeded / total * 100`, rounded to two decimals; empty-day rate and average duration are zero. Average duration is rounded to the nearest whole millisecond, away from zero.

## Database decision

No migration or schema change was required. Existing `users`, `refresh_tokens` and `summary_records` fields and indexes support the complete phase. No existing application database record is changed by installation; mutations occur only when an authorized Admin invokes an endpoint.
