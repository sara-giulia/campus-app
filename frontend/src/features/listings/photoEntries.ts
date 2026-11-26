import { api } from '../../api/client'
import { errorMessage } from '../../app/errorMessage'
import { photoSrc, type GetListingPhotosResponse, type PhotoItem } from './publicTypes'
import { translate } from '../../i18n/store'

// The photos of the listing form: the ones the server already has and the ones the user picked
// and that are not uploaded yet (shown from a local object URL).
export const MAX_PHOTOS = 5
export const MAX_PHOTO_BYTES = 2 * 1024 * 1024
export const PHOTO_TYPES = ['image/jpeg', 'image/png', 'image/webp']

export type PhotoEntry = {
  key: string
  previewUrl: string
  // Why the last upload of this photo failed.
  error?: string
} & ({ kind: 'server'; id: string } | { kind: 'local'; file: File })

export function serverEntry(photo: PhotoItem): PhotoEntry {
  return { key: photo.id, kind: 'server', id: photo.id, previewUrl: photoSrc(photo.url) }
}

export function localEntry(file: File): PhotoEntry {
  return { key: crypto.randomUUID(), kind: 'local', file, previewUrl: URL.createObjectURL(file) }
}

export function releasePreview(entry: PhotoEntry): void {
  if (entry.kind === 'local') URL.revokeObjectURL(entry.previewUrl)
}

// The same checks as the API (it checks again); here they only spare a useless upload.
export function checkPhotoFile(file: File): string | null {
  if (!PHOTO_TYPES.includes(file.type)) return translate('listings.photo.invalidType', { name: file.name })
  if (file.size > MAX_PHOTO_BYTES) return translate('listings.photo.tooLarge', { name: file.name })
  return null
}

export async function loadServerPhotos(listingId: string, signal?: AbortSignal): Promise<PhotoEntry[]> {
  const data = await api.get<GetListingPhotosResponse>(`/listings/${listingId}/photos`, { signal })
  // A generated image is not a photo of the seller: it is replaced when the first real photo comes.
  return data.items.filter((p) => !p.isGenerated).map(serverEntry)
}

// Uploads the local photos one by one, in the order of the list (the server puts each one last).
// An uploaded photo becomes a server entry at once, so a retry never uploads it twice. Stops at the
// first failure, marks that photo and returns the message (null when everything is uploaded) with
// the list as it is now.
export async function uploadPending(
  listingId: string,
  photos: PhotoEntry[],
  setPhotos: (photos: PhotoEntry[]) => void,
): Promise<{ photos: PhotoEntry[]; error: string | null }> {
  const current = [...photos]
  for (let i = 0; i < current.length; i++) {
    const entry = current[i]
    if (entry.kind !== 'local') continue
    try {
      const form = new FormData()
      form.append('file', entry.file)
      const created = await api.post<PhotoItem>(`/listings/${listingId}/photos`, form)
      releasePreview(entry)
      current[i] = serverEntry(created)
      setPhotos([...current])
    } catch (err) {
      const message = errorMessage(err)
      current[i] = { ...entry, error: message }
      setPhotos([...current])
      return { photos: current, error: translate('listings.photo.uploadFailed', { number: i + 1, name: entry.file.name, message }) }
    }
  }
  return { photos: current, error: null }
}

// Makes the photos of the draft on the server the ones of the form: deletes the server photos that are
// no longer in the form, uploads the missing ones, then sends the order of the form
// (PUT /listings/{id}/photos/order). `serverIds` is what the server has, in its order; it is kept up to
// date after each step, so a retry after a failure starts from what is really on the server.
// Returns the upload message (null when the draft now matches the form); a failed delete or order
// call throws the ApiError.
export async function syncPhotos(
  listingId: string,
  photos: PhotoEntry[],
  serverIds: { current: string[] },
  setPhotos: (photos: PhotoEntry[]) => void,
): Promise<string | null> {
  // 1. the photos that were removed from the form
  const kept = new Set(photos.flatMap((p) => (p.kind === 'server' ? [p.id] : [])))
  for (const photoId of serverIds.current.filter((known) => !kept.has(known))) {
    await api.delete(`/listings/${listingId}/photos/${photoId}`)
    serverIds.current = serverIds.current.filter((known) => known !== photoId)
  }

  // 2. the new photos, one by one, in order (the server puts each new photo last)
  const { photos: uploaded, error } = await uploadPending(listingId, photos, (next) => {
    setPhotos(next)
    const known = new Set(serverIds.current)
    serverIds.current = [...serverIds.current, ...next.flatMap((p) => (p.kind === 'server' && !known.has(p.id) ? [p.id] : []))]
  })
  if (error) return error

  // 3. the order of the form
  const order = uploaded.flatMap((p) => (p.kind === 'server' ? [p.id] : []))
  if (order.length > 1 && order.some((photoId, i) => photoId !== serverIds.current[i])) {
    await api.put(`/listings/${listingId}/photos/order`, { photoIds: order })
    serverIds.current = order
  }
  return null
}
