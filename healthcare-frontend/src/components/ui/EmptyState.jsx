import { Inbox } from 'lucide-react';
import Button from './Button';

/**
 * EmptyState — placeholder for zero-data / error-with-retry views.
 * @param {{ message: string, icon?: ReactNode, actionLabel?: string, onAction?: () => void }} props
 */
export default function EmptyState({ message, icon, actionLabel, onAction }) {
  return (
    <div
      className="flex flex-col items-center justify-center py-12 sm:py-16 px-4 text-center min-w-0"
      role="status"
    >
      <div className="w-12 h-12 rounded-full bg-surface flex items-center justify-center mb-4" aria-hidden="true">
        {icon || <Inbox size={24} className="text-text-muted" />}
      </div>
      <p className="text-text-muted text-sm mb-4 max-w-md break-words m-0">{message}</p>
      {actionLabel && onAction && (
        <Button onClick={onAction} size="md" className="min-w-[11rem] sm:min-w-0">
          {actionLabel}
        </Button>
      )}
    </div>
  );
}
