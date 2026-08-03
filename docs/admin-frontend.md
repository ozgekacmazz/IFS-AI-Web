# Admin Frontend

## Routes and access

The React application exposes `/admin`, `/admin/users` and `/admin/logs`. The routes are nested under the existing authenticated and Admin-only guards; non-Admin users are redirected to the normal application. The application keeps using the shared in-memory access token and refresh flow, and does not introduce another HTTP client or browser token storage.

## Overview

The overview combines the seven-day statistics and read-only prompt metadata endpoints. It shows interval totals, success rate, average duration, distinct active users, provider/model totals and all seven UTC calendar days, including zero-value days. The lightweight chart has a semantic table alternative and does not require a chart dependency. Prompt instructions are never requested or exposed.

## User management

The user screen uses server-side pagination, search, role and active-status filters. Creation uses named roles and shared backend validation. Status changes require confirmation, self-deactivation is disabled in the interface, and backend conflict responses remain authoritative. Password reset asks only for the new password and confirmation, clears controlled password values on cancel or success, and explains that refresh sessions are revoked while an already-issued access JWT may remain valid until expiry.

## AI operation logs

The log screen sends pagination and filters to the server and renders only the bounded previews returned by the API. It never requests full source or summary content. Failed operations display the privacy explanation, and expired or unavailable previews are described without implying that retained content exists.

## Accessibility and responsive behavior

Navigation, filters, forms, status messages and dialogs use native semantics and visible focus styles. Dialogs support Escape, receive focus on open and restore it on close. Status is communicated with text and symbols rather than color alone. On narrow screens, tabular data is presented as labelled cards while the statistics chart keeps its screen-reader table.

## Verification

Frontend tests cover route authorization, overview rendering, server-side filters and stale-request cancellation, creation validation and duplicate mapping, confirmation and password-reset behavior, privacy-safe logs and expired previews. Run `npm run lint`, `npm test -- --run` and `npm run build` from `frontend`.
