import { forwardRef } from 'react';

/**
 * Input — text field with label, error(s), and helper text.
 * Supports react-hook-form via ref forwarding.
 * @param {{ label?: string, error?: string | string[], helperText?: string, id?: string }} props
 */
const Input = forwardRef(function Input(
  { label, error, helperText, className = '', id, name, ...props },
  ref,
) {
  const inputId = id || name || (label ? label.toLowerCase().replace(/\s+/g, '-') : undefined);
  const errorList = !error ? [] : Array.isArray(error) ? error.filter(Boolean) : [error];
  const hasError = errorList.length > 0;
  return (
    <div className="mb-4">
      {label && (
        <label htmlFor={inputId} className="block text-sm font-medium text-text mb-1.5">
          {label}
        </label>
      )}
      <input
        ref={ref}
        id={inputId}
        name={name}
        className={`w-full min-h-10 px-3 py-2.5 rounded-md border bg-white text-text text-sm placeholder:text-text-light transition-all duration-150 ease-in-out focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none ${
          hasError ? 'border-status-cancelled-text' : 'border-border'
        } ${className}`}
        aria-invalid={hasError ? 'true' : undefined}
        aria-describedby={hasError ? `${inputId}-error` : helperText ? `${inputId}-helper` : undefined}
        {...props}
      />
      {hasError && (
        <div id={`${inputId}-error`} className="mt-1 space-y-0.5" role="alert">
          {errorList.map((msg, i) => (
            <p key={`${i}-${msg}`} className="text-xs text-status-cancelled-text m-0">{msg}</p>
          ))}
        </div>
      )}
      {!hasError && helperText && (
        <p id={`${inputId}-helper`} className="mt-1 text-xs text-text-muted">{helperText}</p>
      )}
    </div>
  );
});

export default Input;
