import { api } from '../../api/client'
import { errorMessage } from '../../app/errorMessage'
import { photoSrc, type GetListingPhotosResponse, type PhotoItem } from './publicTypes'

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
  if (!PHOTO_TYPES.includes(file.type)) return `„${file.name}": sunt acceptate doar poze JPEG, PNG sau WebP.`
  if (file.size > MAX_PHOTO_BYTES) return `„${file.name}": poza poate avea cel mult 2 MB.`
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
      return { photos: current, error: `Poza ${i + 1} (${entry.file.name}) nu a fost încărcată: ${message}` }
    }
  }
  return { photos: current, error: null }
}
