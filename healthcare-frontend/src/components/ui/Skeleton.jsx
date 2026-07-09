/**
 * Skeleton — placeholder shimmer for loading states.
 * @param {{ variant?: 'text'|'card'|'circle'|'table-row', width?: string, height?: string, className?: string }} props
 */
export default function Skeleton({ variant = 'text', width, height, className = '' }) {
  const base = 'animate-pulse bg-neutral-200 rounded-md';

  if (variant === 'circle') {
    return (
      <div
        className={`${base} rounded-full shrink-0 ${className}`}
        style={{ width: width || '40px', height: height || '40px' }}
      />
    );
  }

  if (variant === 'card') {
    return (
      <div className={`${base} rounded-xl p-5 ${className}`} style={{ width: width || '100%', height: height || '160px' }}>
        <div className="animate-pulse bg-neutral-300 rounded h-4 w-3/5 mb-3" />
        <div className="animate-pulse bg-neutral-300 rounded h-3 w-4/5 mb-2" />
        <div className="animate-pulse bg-neutral-300 rounded h-3 w-2/5" />
      </div>
    );
  }

  if (variant === 'table-row') {
    return (
      <div className={`flex items-center gap-4 py-3 ${className}`}>
        <div className="animate-pulse bg-neutral-200 rounded h-4 w-1/4" />
        <div className="animate-pulse bg-neutral-200 rounded h-4 w-1/4" />
        <div className="animate-pulse bg-neutral-200 rounded h-4 w-1/5" />
        <div className="animate-pulse bg-neutral-200 rounded h-4 w-1/6" />
      </div>
    );
  }

  /* default: text line */
  return (
    <div
      className={`${base} ${className}`}
      style={{ width: width || '100%', height: height || '14px' }}
    />
  );
}
