# CampusMarket web frontend

React 19 + Vite + TypeScript, with React Router. It talks to the CampusMarket API.

## Run

1. Start the API: `dotnet run --project src/CampusMarket.Api --launch-profile http` (from the repository root), on `http://localhost:5112`.
2. In `frontend/`: `npm install` once, then `npm run dev`. Open `http://localhost:5173`.
3. Log in with the Admin from `appsettings.Development.json` or with an account made through `POST /api/v1/auth/register`.

Other commands: `npm run build` (type check + production build in `dist/`), `npm run lint` (oxlint).

The API address comes from `VITE_API_URL` (copy `.env.example` to `.env.local`); the default is `http://localhost:5112`. The API must allow the frontend's origin in `Cors:AllowedOrigins`.

## Folders

| Folder | What is there | Who changes it |
|---|---|---|
| `src/api/` | the API client: base URL, token, automatic refresh, error format, auth calls | Anca |
| `src/app/` | the frame of the app: routes, top menu, login, `useAuth`, `RequireAuth` | Anca (others only add lines, see below) |
| `src/app/ui/` | the shared UI kit: buttons, fields, cards, alerts, badges, chips, pagination, icons, theme | Anca (ask before changing a component; adding one is fine) |
| `src/features/<feature>/` | the pages of one feature (`listings/`, `reservations/`, `notifications/`, ...) | the owner of the feature |

## Look and UI kit

A simple, warm look: cream background, warm white panels with a thin beige border, one caramel-brown accent, the Inter font (installed from npm, no external CDN). Pages keep their content centred. The moon button in the top bar switches to the dark theme; the choice is remembered.

- Colours, radii and shadows are CSS variables in `src/index.css` (`--accent`, `--surface`, `--line`, `--muted`, ...). Use the variables, never fixed colours, so both themes work.
- Build pages from the components in `src/app/ui/`: `import { Button, Card, TextField, Alert, Pagination } from '../../app/ui'`.

| Component | Use |
|---|---|
| `Button`, `ButtonLink` | `variant` primary / secondary / ghost / danger, `size` sm / md / lg, `loading`, `block` |
| `TextField` | label, `hint`, `error`; `type="password"` gets a show/hide button |
| `Card`, `PageHeader` | a white panel; a page title with a small label above it and actions on the right |
| `Alert`, `Badge`, `EmptyState` | messages (`tone` error / success / info), small status labels, "nothing here yet" |
| `Chip` | a rounded label for categories and filters (`active` for the selected one) |
| `Loading`, `Spinner` | waiting for the API |
| `Pagination` | page numbers for the paged API answers |
| icons in `Icons.tsx` | line icons, 24×24, `size` prop |

- Page styles go in a `.css` file next to the page (see `src/app/pages/home.css`), with class names that start with the page name.

## Adding a page

1. Create `src/features/<feature>/` with the page components and a `routes.tsx`:
   ```tsx
   export const reservationsRoutes: RouteObject[] = [
     { path: 'reservations', element: <RequireAuth><MyReservationsPage /></RequireAuth> },
   ]
   ```
2. In `src/app/router.tsx`, import it and add `...reservationsRoutes` to `featureRoutes` (one line).
3. For a menu link, add one line to `src/app/menu.ts`: `{ to: '/reservations', label: 'Rezervările mele', show: 'user' }`. `show` is `'always'`, `'guest'` (only logged out), `'user'` (only logged in) or `'Admin'`.

In `router.tsx` and `menu.ts` only add lines; do not reorder, so two PRs do not conflict.

## Calling the API

```ts
import { api, ApiError } from '../../api/client'

const page = await api.get<ListingsPage>('/listings?page=1&pageSize=20')
await api.post('/listings/' + id + '/reservations')
```

- Paths start after `/api/v1`.
- When someone is logged in, the token is added automatically. The client refreshes it 30 seconds before it expires and, if the API still answers 401, refreshes once and repeats the request. Requests that get a 401 at the same time share one refresh (the API rotates the refresh token). If the refresh fails, the user is logged out and `RequireAuth` sends them to `/login`.
- Errors come as `ApiError` with `status`, `code` (the API's error code, for example `Listing.NotOwner`) and `message`. Both error shapes of the API (`{ code, message, type }` and ProblemDetails) end up the same. When the API cannot be reached, `code` is `Network.Unreachable`.
- For login, register and logout use `useAuth()` or `src/api/auth.ts`, not `api.post` directly.

## Login state

- `const { user, login, logout } = useAuth()` from `src/app/auth/AuthContext.ts`. `user` is `{ id, email, role }` from the access token, or `null`.
- `<RequireAuth>` for pages that need login, `<RequireAuth role="Admin">` for Admin pages. After login the user goes back to the page they wanted.
- The tokens are kept in `localStorage` (`campusmarket.tokens`), so a page refresh keeps the user logged in; logging out in one tab logs out the others too.
