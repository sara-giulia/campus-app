import { useId, useState, type FormEvent } from 'react'
import { api } from '../../api/client'
import { ApiError } from '../../api/errors'
import { errorMessage } from '../../app/errorMessage'
import { Alert, Button } from '../../app/ui'
import './reviews.css'
import { useT } from '../../i18n/LanguageContext'
import { hasKey, translate } from '../../i18n/store'

// Mirrors Review.MaxCommentLength in the API.
const MAX_COMMENT_LENGTH = 500

// The text of the errors POST /listings/{id}/reviews can return is in the dictionaries: "reviews.error.<code>".
function reviewError(error: ApiError): string {
  const key = `reviews.error.${error.code}`
  return hasKey(key) ? translate(key, { max: MAX_COMMENT_LENGTH }) : errorMessage(error)
}

interface ReviewFormProps {
  listingId: string
  listingTitle: string
  onReviewed: () => void
  onCancel: () => void
}

// Stars (1-5) and an optional comment for a completed purchase. On success, or when the sale
// was already reviewed, the parent hides the "Lasă o recenzie" button.
export function ReviewForm({ listingId, listingTitle, onReviewed, onCancel }: ReviewFormProps) {
  const t = useT()
  const commentId = useId()
  const ratingName = useId()
  const [rating, setRating] = useState(0)
  const [hover, setHover] = useState(0)
  const [comment, setComment] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (rating < 1) {
      setError(t('reviews.error.Review.InvalidRating'))
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
      setError(err instanceof ApiError ? reviewError(err) : errorMessage(err))
    } finally {
      setSubmitting(false)
    }
  }

  const shown = hover || rating

  return (
    <form className="review-form" onSubmit={(e) => void handleSubmit(e)}>
      <p className="review-form__title">{t('reviews.form.question', { title: listingTitle })}</p>
      <fieldset className="review-form__stars" onMouseLeave={() => setHover(0)}>
        <legend className="review-form__legend">{t('reviews.form.rating')}</legend>
        {[1, 2, 3, 4, 5].map((value) => (
          <label
            key={value}
            className={value <= shown ? 'review-form__star review-form__star--on' : 'review-form__star'}
            onMouseEnter={() => setHover(value)}
          >
            <input
              type="radio"
              name={ratingName}
              value={value}
              checked={rating === value}
              onChange={() => setRating(value)}
              className="review-form__star-input"
              aria-label={t('reviews.form.starsLabel', { value })}
            />
            <span aria-hidden="true">★</span>
          </label>
        ))}
        <span className="review-form__rating-label">{t(`reviews.rating.${shown as 0 | 1 | 2 | 3 | 4 | 5}`)}</span>
      </fieldset>
      <label htmlFor={commentId} className="review-form__label">
        {t('reviews.form.comment')}
      </label>
      <textarea
        id={commentId}
        className="review-form__textarea"
        maxLength={MAX_COMMENT_LENGTH}
        value={comment}
        placeholder={t('reviews.form.commentPlaceholder')}
        onChange={(e) => setComment(e.target.value)}
      />
      <small className="review-form__counter">
        {comment.length}/{MAX_COMMENT_LENGTH}
      </small>
      {error && <Alert tone="error">{error}</Alert>}
      <div className="review-form__buttons">
        <Button type="button" variant="ghost" size="sm" onClick={onCancel} disabled={submitting}>
          {t('reviews.form.cancel')}
        </Button>
        <Button type="submit" size="sm" loading={submitting} disabled={submitting}>
          {submitting ? t('reviews.form.sending') : t('reviews.form.submit')}
        </Button>
      </div>
    </form>
  )
}
