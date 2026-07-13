import { forwardRef } from 'react';

/**
 * Textarea — multi-line text input with label, error(s), and helper text.
 */
const Textarea = forwardRef(function Textarea(
  { label, error, helperText, className = '', id, name, ...props },
  ref,
) {
  const inputId = id || name || (label ? label.toLowerCase().replace(/\s+/g, '-') : undefined);
  const errorList = !error ? [] : Array.isArray(error) ? error.filter(Boolean) : [error];
  const hasError = errorList.length > 0;
  return (
    <div className="mb-4">
      {label && (
        <label htmlFor={inputId} className="block text-sm font-medium text-text mb-1.5">{label}</label>
      )}
      <textarea
        ref={ref}
        id={inputId}
        name={name}
        className={`w-full px-3 py-2.5 rounded-md border bg-white text-text text-sm placeholder:text-text-light transition-all duration-150 ease-in-out focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none resize-y min-h-[88px] ${
          hasError ? 'border-status-cancelled-text' : 'border-border'
        } ${className}`}
        aria-invalid={hasError ? 'true' : undefined}
        {...props}
      />
      {hasError && (
        <div className="mt-1 space-y-0.5" role="alert">
          {errorList.map((msg) => (
            <p key={msg} className="text-xs text-status-cancelled-text m-0">{msg}</p>
          ))}
        </div>
      )}
      {!hasError && helperText && <p className="mt-1 text-xs text-text-muted">{helperText}</p>}
    </div>
  );
});

export default Textarea;
