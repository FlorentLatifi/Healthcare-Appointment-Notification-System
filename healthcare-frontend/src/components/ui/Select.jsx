/**
 * Select — dropdown with label, error, and helper text.
 */
export default function Select({ label, error, helperText, className = '', children, ...props }) {
  const inputId = label ? label.toLowerCase().replace(/\s+/g, '-') : undefined;
  return (
    <div className="mb-4">
      {label && (
        <label htmlFor={inputId} className="block text-sm font-medium text-text mb-1.5">{label}</label>
      )}
      <select
        id={inputId}
        className={`w-full min-h-10 px-3 py-2.5 rounded-md border bg-white text-text text-sm transition-all duration-150 ease-in-out focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none ${
          error ? 'border-status-cancelled-text' : 'border-border'
        } ${className}`}
        aria-invalid={error ? 'true' : undefined}
        {...props}
      >
        {children}
      </select>
      {error && <p className="mt-1 text-xs text-status-cancelled-text" role="alert">{error}</p>}
      {!error && helperText && <p className="mt-1 text-xs text-text-muted">{helperText}</p>}
    </div>
  );
}
