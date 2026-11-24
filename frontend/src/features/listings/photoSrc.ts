import { API_URL } from '../../api/config'

// The API answers photo urls as "/api/v1/photos/{id}". When the frontend and the API are on
// different origins (Vite on :5173 in development) the url needs the API's address in front.
export function photoSrc(url: string): string {
  return `${API_URL}${url}`
}
