/**
 * Card — container with responsive padding, consistent rounding, and optional hover/click.
 * When onClick is provided, the card is keyboard-accessible (Enter/Space) and exposes button semantics.
 * @param {{ hover?: boolean, onClick?: () => void, className?: string, as?: string }} props
 */
export default function Card({
  className = '',
  children,
  onClick,
  hover = false,
  role,
  tabIndex,
  onKeyDown,
  ...props
}) {
  const isInteractive = typeof onClick === 'function';
  const interactiveRole = role ?? (isInteractive ? 'button' : undefined);
  const interactiveTabIndex = tabIndex ?? (isInteractive ? 0 : undefined);

  const handleKeyDown = (e) => {
    onKeyDown?.(e);
    if (!isInteractive || e.defaultPrevented) return;
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      onClick(e);
    }
  };

  return (
    <div
      className={`bg-white rounded-xl border border-border-light shadow-card p-4 sm:p-5 transition-all duration-200 ease-in-out min-w-0 max-w-full ${
        hover || isInteractive
          ? 'hover:shadow-hover hover:-translate-y-0.5 cursor-pointer focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary'
          : ''
      } ${className}`}
      onClick={onClick}
      onKeyDown={isInteractive || onKeyDown ? handleKeyDown : undefined}
      role={interactiveRole}
      tabIndex={interactiveTabIndex}
      {...props}
    >
      {children}
    </div>
  );
}
