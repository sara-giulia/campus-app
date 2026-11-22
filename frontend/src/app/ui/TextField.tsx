import { useId, useState, type InputHTMLAttributes } from 'react'
import { EyeIcon, EyeOffIcon } from './Icons'

type TextFieldProps = InputHTMLAttributes<HTMLInputElement> & {
  label: string
  hint?: string
  error?: string | null
}

// A labelled input with an optional hint and error. type="password" gets a show/hide button.
// <TextField label="Email" type="email" value={email} onChange={...} error={errors.email} />
export function TextField({ label, hint, error, type = 'text', id, className, ...props }: TextFieldProps) {
  const autoId = useId()
  const inputId = id ?? autoId
  const [showPassword, setShowPassword] = useState(false)
  const isPassword = type === 'password'

  const describedBy = [hint && `${inputId}-hint`, error && `${inputId}-error`].filter(Boolean).join(' ') || undefined

  return (
    <div className={['field', error && 'field--error', className].filter(Boolean).join(' ')}>
      <label className="field__label" htmlFor={inputId}>
        {label}
      </label>
      <div className="field__control">
        <input
          id={inputId}
          className={isPassword ? 'field__input field__input--with-action' : 'field__input'}
          type={isPassword && showPassword ? 'text' : type}
          aria-invalid={error ? true : undefined}
          aria-describedby={describedBy}
          {...props}
        />
        {isPassword && (
          <button
            type="button"
            className="field__action"
            onClick={() => setShowPassword((v) => !v)}
            aria-label={showPassword ? 'Ascunde parola' : 'Arată parola'}
            title={showPassword ? 'Ascunde parola' : 'Arată parola'}
          >
            {showPassword ? <EyeOffIcon /> : <EyeIcon />}
          </button>
        )}
      </div>
      {hint && !error && (
        <span id={`${inputId}-hint`} className="field__hint">
          {hint}
        </span>
      )}
      {error && (
        <span id={`${inputId}-error`} className="field__error">
          {error}
        </span>
      )}
    </div>
  )
}
