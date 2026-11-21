// Where the API runs. Set VITE_API_URL in frontend/.env.local to change it
// (see .env.example); the default is the API's http profile from launchSettings.json.
export const API_URL: string = (import.meta.env.VITE_API_URL ?? 'http://localhost:5112').replace(/\/+$/, '')

// Every endpoint lives under /api/v1 (except /health).
export const API_PREFIX = '/api/v1'
