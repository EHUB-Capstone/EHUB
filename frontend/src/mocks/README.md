# Frontend mock API

The mock layer covers the API contracts introduced after commit `da65cde`
(22 July 2026) for class management, subjects, managed users, teams, project
directions, the admin dashboard, and tracking.

It is frontend-only. Unmatched requests pass through to the configured backend,
so older modules continue to work normally. When mocks are enabled, authentication
is also mocked so protected screens can be tested without starting the backend.

Use password `Mock123!` with one of these fixture accounts:

- `admin@ehub.local`
- `giang.lecturer@ehub.local`
- `khoa.mentor@ehub.local`
- `se200001@fpt.edu.vn`
- `yen.mentor@ehub.local` (pending approval)
- `rejected.mentor@ehub.local` (rejected)
- `blocked.mentor@ehub.local` (blocked)
- `inactive.lecturer@ehub.local` (inactive)

The Google endpoint accepts a real Google JWT payload in mock mode, or the
deterministic test token `mock-google:<fixture-email>` (for example,
`mock-google:admin@ehub.local`). Use
`mock-google-unverified:<fixture-email>` to exercise the unverified-email error.

Enable it in `.env.local`:

```env
VITE_ENABLE_API_MOCKS=true
```

Mock mutations are stored under `ehub_mock_api_state_v3` in browser
`localStorage`. To restore the original fixtures, run this in DevTools:

```js
window.__EHUB_MOCK_API__.reset()
```

Set the flag to `false` (or remove it) to use the real API for every request.
