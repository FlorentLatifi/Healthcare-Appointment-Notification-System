/**
 * Card — container with responsive padding, consistent rounding, and optional hover.
 * @param {{ hover?: boolean, onClick?: () => void, className?: string }} props
 */
export default function Card({ className = '', children, onClick, hover = false, ...props }) {
  return (
    <div
      className={`bg-white rounded-xl border border-border-light shadow-card p-4 sm:p-5 transition-all duration-200 ease-in-out ${
        hover ? 'hover:shadow-hover hover:-translate-y-0.5 cursor-pointer' : ''
      } ${className}`}
      onClick={onClick}
      {...props}
    >
      {children}
    </div>
  );
}
