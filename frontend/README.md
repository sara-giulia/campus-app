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

The look follows Sara's page sketches (`Campus Hub – schițe pagini.html`, outside the repo): white background, one coral-red accent, the Figtree font (installed from npm, no external CDN), rounded corners, the `campusmarket` wordmark in coral, a light grey footer. Until listings have photos, pastel tiles (`--tile-*` colours) stand in for the pictures. The moon button in the top bar switches to the dark theme; the choice is remembered.

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
- A file upload sends a `FormData` body: `api.post('/listings/' + id + '/photos', form)`; the client leaves the multipart `Content-Type` to the browser. Photo urls from the API (`/api/v1/photos/{id}`) go through `photoSrc()` in `features/listings/photoSrc.ts`, which puts the API address in front.
- `errorMessage(error)` in `src/app/errorMessage.ts` gives the Romanian text for the error codes of the public pages (falls back to the API's message).

## Pages

The top bar has Acasă, Anunțuri, Publică anunț, Mesaje and Notificări; the personal pages and the Admin pages (`place: 'account'` in `src/app/menu.ts`) are in the menu that opens from the avatar, and everything is in the phone menu below 900px.

| Address | Page | Who |
|---|---|---|
| `/` | landing + the newest listings | everyone |
| `/listings?search=&category=&condition=&minPrice=&maxPrice=&sort=&page=` | search with filters, sorting and pages (the filters are in the address) | everyone |
| `/listings/:id` | photos, details, seller, similar listings; reserve, save, write to the seller, report; the seller adds and removes photos | everyone (actions need login) |
| `/members/:id` | a seller's active listings and reviews | everyone |
| `/messages`, `/messages/:id` | my conversations and one conversation (refreshed every 5 seconds) | logged in |
| `/my/favorites` | the listings I saved | logged in |
| `/admin/moderation` | reported listings, hide one | Admin |
| `/admin/categories` | the 5 base categories: change the display name, the description and the active flag | Admin |
| `/listings/new`, `/my/listings`, `/my/listings/:id/edit`, `/my/reservations`, `/profile`, `/register`, `/forgot-password`, `/reset-password` | publish and edit, my listings, my reservations, my profile, account pages (Sara) | see the routes |

## Login state

- `const { user, login, logout } = useAuth()` from `src/app/auth/AuthContext.ts`. `user` is `{ id, email, role }` from the access token, or `null`.
- `<RequireAuth>` for pages that need login, `<RequireAuth role="Admin">` for Admin pages. After login the user goes back to the page they wanted.
- The tokens are kept in `localStorage` (`campusmarket.tokens`), so a page refresh keeps the user logged in; logging out in one tab logs out the others too.
