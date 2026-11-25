import { useEffect, useRef, useState, type ChangeEvent } from 'react'
import { Alert, Button } from '../../app/ui'
import { checkPhotoFile, localEntry, MAX_PHOTOS, releasePreview, type PhotoEntry } from './photoEntries'

interface PhotoPickerProps {
  photos: PhotoEntry[]
  onChange: (photos: PhotoEntry[]) => void
  disabled?: boolean
}

// Photos of a listing: pick files, see them at once (local previews), remove them and move them up or
// down; the first one is the cover. Nothing is sent here: the page uploads them when the form is saved.
export function PhotoPicker({ photos, onChange, disabled = false }: PhotoPickerProps) {
  const [problems, setProblems] = useState<string[]>([])

  // The local previews are freed when the form goes away.
  const latest = useRef(photos)
  useEffect(() => {
    latest.current = photos
  })
  useEffect(() => () => latest.current.forEach(releasePreview), [])

  function add(event: ChangeEvent<HTMLInputElement>) {
    const files = Array.from(event.target.files ?? [])
    event.target.value = ''
    const next = [...photos]
    const found: string[] = []
    for (const file of files) {
      if (next.length >= MAX_PHOTOS) {
        found.push(`Un anunț poate avea cel mult ${MAX_PHOTOS} poze.`)
        break
      }
      const problem = checkPhotoFile(file)
      if (problem) found.push(problem)
      else next.push(localEntry(file))
    }
    setProblems(found)
    if (next.length !== photos.length) onChange(next)
  }

  function remove(index: number) {
    releasePreview(photos[index])
    setProblems([])
    onChange(photos.filter((_, i) => i !== index))
  }

  function move(index: number, by: -1 | 1) {
    const target = index + by
    if (target < 0 || target >= photos.length) return
    const next = [...photos]
    ;[next[index], next[target]] = [next[target], next[index]]
    onChange(next)
  }

  return (
    <div className="photo-picker">
      <div className="photo-picker__head">
        <span className="field__label">Poze</span>
        <span className="photo-picker__hint">
          {photos.length} / {MAX_PHOTOS}
        </span>
      </div>

      {photos.length > 0 && (
        <ul className="photo-picker__list">
          {photos.map((photo, index) => (
            <li key={photo.key} className={photo.error ? 'photo-picker__item photo-picker__item--failed' : 'photo-picker__item'}>
              <img src={photo.previewUrl} alt={`Poza ${index + 1}`} />
              {index === 0 && <span className="photo-picker__cover">Copertă</span>}
              <div className="photo-picker__buttons">
                <Button variant="ghost" size="sm" disabled={disabled || index === 0} aria-label={`Mută poza ${index + 1} mai sus`} onClick={() => move(index, -1)}>
                  ←
                </Button>
                <Button variant="ghost" size="sm" disabled={disabled || index === photos.length - 1} aria-label={`Mută poza ${index + 1} mai jos`} onClick={() => move(index, 1)}>
                  →
                </Button>
                <Button variant="ghost" size="sm" disabled={disabled} aria-label={`Șterge poza ${index + 1}`} onClick={() => remove(index)}>
                  Șterge
                </Button>
              </div>
              {photo.error && <span className="photo-picker__error">{photo.error}</span>}
            </li>
          ))}
        </ul>
      )}

      {photos.length < MAX_PHOTOS && (
        <label className={disabled ? 'btn btn--secondary btn--sm photo-picker__add is-disabled' : 'btn btn--secondary btn--sm photo-picker__add'}>
          Adaugă poze
          <input type="file" accept="image/jpeg,image/png,image/webp" multiple onChange={add} disabled={disabled} hidden />
        </label>
      )}
      <span className="photo-picker__hint">Maxim {MAX_PHOTOS} poze, JPEG, PNG sau WebP, cel mult 2 MB fiecare. Prima poză este coperta.</span>
      {problems.map((p) => (
        <Alert key={p} tone="error">
          {p}
        </Alert>
      ))}
    </div>
  )
}
