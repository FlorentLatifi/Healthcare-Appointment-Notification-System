/**
 * Card — container with consistent rounding, shadow, and optional hover elevation.
 * @param {{ hover?: boolean, onClick?: () => void, className?: string }} props
 */
export default function Card({ className = '', children, onClick, hover = false, ...props }) {
  return (
    <div
      className={`bg-white rounded-xl border border-border-light shadow-card p-5 transition-all duration-200 ease-in-out ${
        hover ? 'hover:shadow-hover hover:-translate-y-0.5 cursor-pointer' : ''
      } ${className}`}
      onClick={onClick}
      {...props}
    >
      {children}
    </div>
  );
}
