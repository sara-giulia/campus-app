import { useId, useState, type ChangeEvent, type FormEvent } from 'react'
import { api } from '../../api/client'
import { ApiError } from '../../api/errors'
import { errorMessage } from '../../app/errorMessage'
import { Alert, Badge, Button, Card, Loading, PageHeader, TextField } from '../../app/ui'
import { reloadCategories, useCategories, type CategoryItem } from '../listings/categories'
import { placeCategoryError, type CategoryFieldErrors } from './errors'
import '../listings/listings.css'
import './admin-categories.css'

// Admin: the 5 base categories (the ML models use them, so they cannot be added or deleted).
// An Admin changes the display name, the description and whether new listings can use the
// category. Listings already in a deactivated category stay as they are.
export function AdminCategoriesPage() {
  const { categories, loaded, error } = useCategories({ fresh: true })
  const activeCount = categories.filter((c) => c.isActive).length

  return (
    <div className="listings-page admin-categories">
      <PageHeader
        eyebrow="Administrare"
        title="Categorii"
        lead="Numele de aici apar peste tot în aplicație: în căutare, pe carduri și în formularul de publicare. Dezactivează o categorie când nu mai vrei anunțuri noi în ea (cele existente rămân). Cele 5 categorii sunt fixe, pentru că modelele ML le folosesc la sugestii."
      />
      {error !== null && <Alert tone="error">{errorMessage(error)}</Alert>}
      {!loaded && error === null && <Loading text="Se încarcă categoriile…" />}
      {loaded &&
        categories.map((category) => (
          <CategoryEditor key={category.key} category={category} isLastActive={category.isActive && activeCount === 1} />
        ))}
    </div>
  )
}

function CategoryEditor({ category, isLastActive }: { category: CategoryItem; isLastActive: boolean }) {
  const [displayName, setDisplayName] = useState(category.displayName)
  const [description, setDescription] = useState(category.description ?? '')
  const [isActive, setIsActive] = useState(category.isActive)

  const [fieldErrors, setFieldErrors] = useState<CategoryFieldErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)
  const [saving, setSaving] = useState(false)

  const descriptionId = useId()
  const activeId = useId()

  const changed =
    displayName !== category.displayName || description !== (category.description ?? '') || isActive !== category.isActive

  async function save(): Promise<void> {
    setSaving(true)
    try {
      await api.put<void>(`/categories/${category.key}`, {
        displayName: displayName.trim(),
        description: description.trim() === '' ? null : description.trim(),
        isActive,
      })
      // Every page (search, cards, forms) shows the new name and the new active flag.
      await reloadCategories()
      setDisplayName(displayName.trim())
      setDescription(description.trim())
      setSaved(true)
    } catch (err) {
      if (err instanceof ApiError) {
        const placed = placeCategoryError(err)
        setFieldErrors(placed.fields)
        setFormError(placed.form)
      } else {
        setFormError('A apărut o eroare neașteptată.')
      }
    } finally {
      setSaving(false)
    }
  }

  // Returns nothing: save handles its own errors, so the form can just start it.
  function handleSubmit(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault()
    setFieldErrors({})
    setFormError(null)
    setSaved(false)
    save().catch(() => setFormError('A apărut o eroare neașteptată.'))
  }

  function edited<T>(setter: (value: T) => void) {
    return (value: T) => {
      setter(value)
      setSaved(false)
    }
  }

  return (
    <Card className="listings-card">
      <form onSubmit={handleSubmit} className="listings-form" noValidate>
        <div className="admin-categories__head">
          <h2 className="admin-categories__title">
            {category.displayName} <span className="admin-categories__key">({category.key})</span>
          </h2>
          <Badge tone={category.isActive ? 'success' : 'warning'}>{category.isActive ? 'Activă' : 'Inactivă'}</Badge>
        </div>

        <TextField
          label="Nume afișat"
          value={displayName}
          onChange={(e: ChangeEvent<HTMLInputElement>) => edited(setDisplayName)(e.target.value)}
          error={fieldErrors.displayName}
          maxLength={100}
          required
        />

        <div className={fieldErrors.description ? 'field field--error' : 'field'}>
          <label className="field__label" htmlFor={descriptionId}>Descriere</label>
          <textarea
            id={descriptionId}
            className="listings-form__textarea admin-categories__textarea"
            value={description}
            onChange={(e: ChangeEvent<HTMLTextAreaElement>) => edited(setDescription)(e.target.value)}
            maxLength={500}
            aria-invalid={fieldErrors.description ? true : undefined}
            aria-describedby={fieldErrors.description ? `${descriptionId}-error` : undefined}
          />
          {!fieldErrors.description && <span className="field__hint">Opțional. Maxim 500 de caractere.</span>}
          {fieldErrors.description && (
            <span id={`${descriptionId}-error`} className="field__error">
              {fieldErrors.description}
            </span>
          )}
        </div>

        <div className="field">
          <label className="admin-categories__active" htmlFor={activeId}>
            <input
              id={activeId}
              type="checkbox"
              checked={isActive}
              onChange={(e: ChangeEvent<HTMLInputElement>) => edited(setIsActive)(e.target.checked)}
            />
            Activă (se pot publica anunțuri noi în ea)
          </label>
          {isLastActive && <span className="field__hint">Este singura categorie activă, nu poate fi dezactivată.</span>}
        </div>

        {formError && <Alert tone="error">{formError}</Alert>}
        {saved && <Alert tone="success">Categoria a fost salvată.</Alert>}

        <div className="admin-categories__actions">
          <Button type="submit" loading={saving} disabled={!changed || saving}>
            {saving ? 'Se salvează…' : 'Salvează'}
          </Button>
        </div>
      </form>
    </Card>
  )
}
