/**
 * Textarea — multi-line text input with label, error, and helper text.
 */
export default function Textarea({ label, error, helperText, className = '', id, ...props }) {
  const inputId = id || (label ? label.toLowerCase().replace(/\s+/g, '-') : undefined);
  return (
    <div className="mb-4">
      {label && (
        <label htmlFor={inputId} className="block text-sm font-medium text-text mb-1.5">{label}</label>
      )}
      <textarea
        id={inputId}
        className={`w-full px-3 py-2.5 rounded-md border bg-white text-text text-sm placeholder:text-text-light transition-all duration-150 ease-in-out focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none resize-y min-h-[88px] ${
          error ? 'border-status-cancelled-text' : 'border-border'
        } ${className}`}
        aria-invalid={error ? 'true' : undefined}
        {...props}
      />
      {error && <p className="mt-1 text-xs text-status-cancelled-text" role="alert">{error}</p>}
      {!error && helperText && <p className="mt-1 text-xs text-text-muted">{helperText}</p>}
    </div>
  );
}
