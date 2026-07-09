import { Loader2 } from 'lucide-react';

const sizes = { sm: 16, md: 24, lg: 32 };
const textSizes = { sm: 'text-xs', md: 'text-sm', lg: 'text-base' };

/**
 * Spinner — loading indicator with optional text.
 * @param {{ size?: 'sm'|'md'|'lg', text?: string, className?: string }} props
 */
export default function Spinner({ size = 'md', text = 'Loading...', className = '' }) {
  return (
    <div className={`flex items-center justify-center py-12 text-text-muted ${className}`}>
      <Loader2 size={sizes[size]} className="animate-spin shrink-0" />
      {text && <span className={`ml-2 ${textSizes[size]}`}>{text}</span>}
    </div>
  );
}
