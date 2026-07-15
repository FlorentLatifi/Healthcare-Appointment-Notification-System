import { forwardRef } from 'react';

/**
 * Select — dropdown with label, error(s), helper text, and aria-describedby.
 * Touch target ≥44px on mobile.
 */
const Select = forwardRef(function Select(
  { label, error, helperText, className = '', children, name, id, required, 'aria-label': ariaLabel, ...props },
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
      <select
        ref={ref}
        id={inputId}
        name={name}
        required={required}
        className={`w-full min-w-0 min-h-11 sm:min-h-10 px-3 py-2.5 rounded-md border bg-white text-text text-sm transition-all duration-150 ease-in-out focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none ${
          hasError ? 'border-status-cancelled-text' : 'border-border'
        } ${className}`}
        aria-invalid={hasError ? 'true' : undefined}
        aria-describedby={describedBy}
        aria-required={required ? 'true' : undefined}
        aria-label={label ? undefined : ariaLabel}
        {...props}
      >
        {children}
      </select>
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

export default Select;
