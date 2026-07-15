import { forwardRef } from 'react';

/**
 * Textarea — multi-line text input with label, error(s), helper text, and aria-describedby.
 */
const Textarea = forwardRef(function Textarea(
  { label, error, helperText, className = '', id, name, required, ...props },
  ref,
) {
  const inputId = id || name || (label ? label.toLowerCase().replace(/\s+/g, '-') : undefined);
  const errorList = !error ? [] : Array.isArray(error) ? error.filter(Boolean) : [error];
  const hasError = errorList.length > 0;
  const errorId = inputId ? `${inputId}-error` : undefined;
  const helperId = inputId ? `${inputId}-helper` : undefined;
  const describedBy = [
    hasError && errorId,
    !hasError && helperText && helperId,
  ].filter(Boolean).join(' ') || undefined;

  return (
    <div className="mb-4 min-w-0">
      {label && (
        <label htmlFor={inputId} className="block text-sm font-medium text-text mb-1.5">
          {label}
          {required && (
            <span className="text-status-cancelled-text ml-0.5">
              <span aria-hidden="true">*</span>
              <span className="sr-only"> (required)</span>
            </span>
          )}
        </label>
      )}
      <textarea
        ref={ref}
        id={inputId}
        name={name}
        required={required}
        className={`w-full min-w-0 px-3 py-2.5 rounded-md border bg-white text-text text-sm placeholder:text-text-muted/80 transition-all duration-150 ease-in-out focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none resize-y min-h-[88px] ${
          hasError ? 'border-status-cancelled-text' : 'border-border'
        } ${className}`}
        aria-invalid={hasError ? 'true' : undefined}
        aria-describedby={describedBy}
        aria-required={required ? 'true' : undefined}
        {...props}
      />
      {hasError && (
        <div id={errorId} className="mt-1 space-y-0.5" role="alert">
          {errorList.map((msg, i) => (
            <p key={`${i}-${msg}`} className="text-xs text-status-cancelled-text m-0">{msg}</p>
          ))}
        </div>
      )}
      {!hasError && helperText && (
        <p id={helperId} className="mt-1 text-xs text-text-muted">{helperText}</p>
      )}
    </div>
  );
});

export default Textarea;
