// Where the API runs. Set VITE_API_URL in frontend/.env.local to change it
// (see .env.example). The default depends on the build mode: in production the
// frontend is served by the API on the same origin, so API_URL is "" and
// requests go to /api/v1/... on the current host; in development the API runs
// on its http profile from launchSettings.json.
const defaultApiUrl: string = import.meta.env.PROD ? '' : 'http://localhost:5112'
export const API_URL: string = withoutTrailingSlashes(import.meta.env.VITE_API_URL ?? defaultApiUrl)

// Every endpoint lives under /api/v1 (except /health).
export const API_PREFIX = '/api/v1'

// "http://localhost:5112/" → "http://localhost:5112" (a loop, not a regex, so it never backtracks).
function withoutTrailingSlashes(url: string): string {
  let end = url.length
  while (end > 0 && url[end - 1] === '/') end--
  return url.slice(0, end)
}
