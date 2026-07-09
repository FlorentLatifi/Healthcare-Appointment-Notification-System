import { Inbox } from 'lucide-react';
import Button from './Button';

/**
 * EmptyState — placeholder for zero-data views.
 * @param {{ message: string, icon?: ReactNode, actionLabel?: string, onAction?: () => void }} props
 */
export default function EmptyState({ message, icon, actionLabel, onAction }) {
  return (
    <div className="flex flex-col items-center justify-center py-16 text-center">
      <div className="w-12 h-12 rounded-full bg-surface flex items-center justify-center mb-4">
        {icon || <Inbox size={24} className="text-text-muted" />}
      </div>
      <p className="text-text-muted text-sm mb-4">{message}</p>
      {actionLabel && (
        <Button onClick={onAction} size="sm">
          {actionLabel}
        </Button>
      )}
    </div>
  );
}
