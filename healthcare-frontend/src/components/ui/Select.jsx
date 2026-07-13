import { forwardRef } from 'react';

/**
 * Select — dropdown with label, error(s), and helper text.
 */
const Select = forwardRef(function Select(
  { label, error, helperText, className = '', children, name, id, ...props },
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
      <select
        ref={ref}
        id={inputId}
        name={name}
        className={`w-full min-h-10 px-3 py-2.5 rounded-md border bg-white text-text text-sm transition-all duration-150 ease-in-out focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none ${
          hasError ? 'border-status-cancelled-text' : 'border-border'
        } ${className}`}
        aria-invalid={hasError ? 'true' : undefined}
        {...props}
      >
        {children}
      </select>
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

export default Select;
