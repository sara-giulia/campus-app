import { useId, useState, type FormEvent } from 'react'
import { api } from '../../api/client'
import { ApiError } from '../../api/errors'
import { errorMessage } from '../../app/errorMessage'
import { Alert, Button } from '../../app/ui'
import './reviews.css'

// Mirrors Review.MaxCommentLength in the API.
const MAX_COMMENT_LENGTH = 500

// Romanian text for the errors POST /listings/{id}/reviews can return.
const REVIEW_ERRORS: Record<string, string> = {
  'Review.NotBuyer': 'Doar cumpărătorul acestui anunț poate lăsa o recenzie.',
  'Review.SaleNotCompleted': 'Poți lăsa o recenzie doar după ce vânzarea a fost confirmată.',
  'Review.AlreadyExists': 'Ai lăsat deja o recenzie pentru această vânzare.',
  'Review.InvalidRating': 'Alege între 1 și 5 stele.',
  'Review.CommentTooLong': `Comentariul poate avea cel mult ${MAX_COMMENT_LENGTH} de caractere.`,
}

const RATING_LABELS = ['', 'Foarte slab', 'Slab', 'OK', 'Bun', 'Excelent']

interface ReviewFormProps {
  listingId: string
  listingTitle: string
  onReviewed: () => void
  onCancel: () => void
}

// Stars (1-5) and an optional comment for a completed purchase. On success, or when the sale
// was already reviewed, the parent hides the "Lasă o recenzie" button.
export function ReviewForm({ listingId, listingTitle, onReviewed, onCancel }: ReviewFormProps) {
  const commentId = useId()
  const [rating, setRating] = useState(0)
  const [hover, setHover] = useState(0)
  const [comment, setComment] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (rating < 1) {
      setError('Alege între 1 și 5 stele.')
      return
    }
    setSubmitting(true)
    setError(null)
    try {
      await api.post(`/listings/${listingId}/reviews`, { rating, comment: comment.trim() || null })
      onReviewed()
    } catch (err) {
      if (err instanceof ApiError && err.code === 'Review.AlreadyExists') {
        onReviewed()
        return
      }
      setError(err instanceof ApiError ? (REVIEW_ERRORS[err.code] ?? errorMessage(err)) : errorMessage(err))
    } finally {
      setSubmitting(false)
    }
  }

  const shown = hover || rating

  return (
    <form className="review-form" onSubmit={(e) => void handleSubmit(e)}>
      <p className="review-form__title">Cum a fost vânzătorul pentru „{listingTitle}”?</p>
      <div className="review-form__stars" role="radiogroup" aria-label="Nota" onMouseLeave={() => setHover(0)}>
        {[1, 2, 3, 4, 5].map((value) => (
          <button
            key={value}
            type="button"
            role="radio"
            aria-checked={rating === value}
            aria-label={`${value} din 5 stele`}
            className={value <= shown ? 'review-form__star review-form__star--on' : 'review-form__star'}
            onMouseEnter={() => setHover(value)}
            onClick={() => setRating(value)}
          >
            ★
          </button>
        ))}
        <span className="review-form__rating-label">{RATING_LABELS[shown]}</span>
      </div>
      <label htmlFor={commentId} className="review-form__label">
        Comentariu (opțional)
      </label>
      <textarea
        id={commentId}
        className="review-form__textarea"
        maxLength={MAX_COMMENT_LENGTH}
        value={comment}
        placeholder="Cum a decurs întâlnirea, starea produsului…"
        onChange={(e) => setComment(e.target.value)}
      />
      <small className="review-form__counter">
        {comment.length}/{MAX_COMMENT_LENGTH}
      </small>
      {error && <Alert tone="error">{error}</Alert>}
      <div className="review-form__buttons">
        <Button type="button" variant="ghost" size="sm" onClick={onCancel} disabled={submitting}>
          Renunță
        </Button>
        <Button type="submit" size="sm" loading={submitting} disabled={submitting}>
          {submitting ? 'Se trimite…' : 'Trimite recenzia'}
        </Button>
      </div>
    </form>
  )
}
