// Where the API runs. Set VITE_API_URL in frontend/.env.local to change it
// (see .env.example); the default is the API's http profile from launchSettings.json.
export const API_URL: string = withoutTrailingSlashes(import.meta.env.VITE_API_URL ?? 'http://localhost:5112')

// Every endpoint lives under /api/v1 (except /health).
export const API_PREFIX = '/api/v1'

// "http://localhost:5112/" → "http://localhost:5112" (a loop, not a regex, so it never backtracks).
function withoutTrailingSlashes(url: string): string {
  let end = url.length
  while (end > 0 && url[end - 1] === '/') end--
  return url.slice(0, end)
}
