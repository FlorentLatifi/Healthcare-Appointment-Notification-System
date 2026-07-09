/**
 * Input — text field with label, error, and helper text.
 * @param {{ label?: string, error?: string, helperText?: string, id?: string }} props
 */
export default function Input({ label, error, helperText, className = '', id, ...props }) {
  const inputId = id || (label ? label.toLowerCase().replace(/\s+/g, '-') : undefined);
  return (
    <div className="mb-4">
      {label && (
        <label htmlFor={inputId} className="block text-sm font-medium text-text mb-1.5">
          {label}
        </label>
      )}
      <input
        id={inputId}
        className={`w-full px-3 py-2 rounded-md border bg-white text-text text-sm placeholder:text-text-light transition-all duration-150 ease-in-out focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none ${
          error ? 'border-status-cancelled-text' : 'border-border'
        } ${className}`}
        aria-invalid={error ? 'true' : undefined}
        aria-describedby={error ? `${inputId}-error` : helperText ? `${inputId}-helper` : undefined}
        {...props}
      />
      {error && (
        <p id={`${inputId}-error`} className="mt-1 text-xs text-status-cancelled-text" role="alert">{error}</p>
      )}
      {!error && helperText && (
        <p id={`${inputId}-helper`} className="mt-1 text-xs text-text-muted">{helperText}</p>
      )}
    </div>
  );
}
