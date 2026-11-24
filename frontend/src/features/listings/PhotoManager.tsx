import { useState, type ChangeEvent } from 'react'
import { api } from '../../api/client'
import { errorMessage } from '../../app/errorMessage'
import { Alert, Button } from '../../app/ui'
import { photoSrc, type PhotoItem } from './publicTypes'

const MAX_PHOTOS = 5
const MAX_BYTES = 2 * 1024 * 1024

// The seller's photo controls on their listing page: add (JPEG, PNG or WebP, up to 2 MB, at most 5)
// and remove. The API checks everything again; the checks here only spare a useless upload.
export function PhotoManager({ listingId, photos, onChange }: { listingId: string; photos: PhotoItem[]; onChange: () => void }) {
  const [busy, setBusy] = useState(false)
  // A generated image does not count towards the limit and is replaced by the first real photo.
  const real = photos.filter((p) => !p.isGenerated)
  const generated = photos.length > real.length
  const [error, setError] = useState<string | null>(null)

  async function upload(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (!file) return
    if (file.size > MAX_BYTES) {
      setError('Poza poate avea cel mult 2 MB.')
      return
    }
    setBusy(true)
    setError(null)
    try {
      const form = new FormData()
      form.append('file', file)
      await api.post(`/listings/${listingId}/photos`, form)
      onChange()
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  async function remove(photo: PhotoItem) {
    if (!window.confirm('Ștergi poza?')) return
    setBusy(true)
    setError(null)
    try {
      await api.delete(`/listings/${listingId}/photos/${photo.id}`)
      onChange()
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="photo-manager">
      <div className="photo-manager__head">
        <h2 className="listing-page__section-title">Pozele anunțului</h2>
        <span className="photo-manager__count">
          {real.length} / {MAX_PHOTOS}
        </span>
      </div>
      {generated && (
        <p className="photo-manager__hint">Acum se vede o imagine generată din titlu și categorie. Dispare când adaugi prima ta poză.</p>
      )}
      {real.length > 0 && (
        <ul className="photo-manager__list">
          {real.map((p) => (
            <li key={p.id}>
              <img src={photoSrc(p.url)} alt="" />
              <Button variant="ghost" size="sm" onClick={() => remove(p)} disabled={busy}>
                Șterge
              </Button>
            </li>
          ))}
        </ul>
      )}
      {real.length < MAX_PHOTOS && (
        <label className={busy ? 'btn btn--secondary btn--sm photo-manager__add is-busy' : 'btn btn--secondary btn--sm photo-manager__add'}>
          {busy ? 'Se încarcă…' : 'Adaugă o poză'}
          <input type="file" accept="image/jpeg,image/png,image/webp" onChange={upload} disabled={busy} hidden />
        </label>
      )}
      <p className="photo-manager__hint">JPEG, PNG sau WebP, cel mult 2 MB fiecare.</p>
      {error && <Alert tone="error">{error}</Alert>}
    </div>
  )
}
