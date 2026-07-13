import { Loader2 } from 'lucide-react';

const variants = {
  primary: 'bg-primary text-white hover:bg-primary-hover shadow-card',
  secondary: 'bg-white text-text border border-border hover:bg-surface',
  danger: 'bg-status-cancelled-bg text-status-cancelled-text border border-status-cancelled-bg hover:brightness-95',
  ghost: 'bg-transparent text-text-secondary hover:bg-surface',
  outline: 'bg-transparent text-primary border border-primary hover:bg-primary-50',
};

/** Mobile-first min heights meet 44×44px touch targets; sm+ can be slightly denser. */
const sizes = {
  sm: 'min-h-11 min-w-11 sm:min-h-9 sm:min-w-0 px-3 py-2 sm:py-1.5 text-xs gap-1.5',
  md: 'min-h-11 min-w-11 sm:min-h-10 sm:min-w-0 px-4 py-2.5 sm:py-2 text-sm gap-2',
  lg: 'min-h-12 min-w-11 sm:min-h-11 sm:min-w-0 px-6 py-3 sm:py-2.5 text-base gap-2',
};

const spinnerSizes = { sm: 14, md: 16, lg: 18 };

/**
 * Button — primary action control.
 * @param {{ variant?: 'primary'|'secondary'|'danger'|'ghost'|'outline', size?: 'sm'|'md'|'lg', loading?: boolean, leftIcon?: ReactNode, rightIcon?: ReactNode }} props
 */
export default function Button({
  variant = 'primary', size = 'md', loading = false, leftIcon, rightIcon,
  className = '', disabled, children, type = 'button', ...props
}) {
  const isDisabled = disabled || loading;
  return (
    <button
      type={type}
      disabled={isDisabled}
      aria-busy={loading ? 'true' : undefined}
      className={`inline-flex items-center justify-center font-medium rounded-md transition-all duration-200 ease-in-out cursor-pointer active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed disabled:active:scale-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary ${variants[variant]} ${sizes[size]} ${className}`}
      {...props}
    >
      {loading ? (
        <Loader2 size={spinnerSizes[size]} className="animate-spin shrink-0" aria-hidden="true" />
      ) : leftIcon ? (
        <span className="shrink-0" aria-hidden="true">{leftIcon}</span>
      ) : null}
      {children}
      {!loading && rightIcon && <span className="shrink-0" aria-hidden="true">{rightIcon}</span>}
    </button>
  );
}
