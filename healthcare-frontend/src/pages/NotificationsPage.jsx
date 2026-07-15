import { useEffect } from 'react';
import toast from 'react-hot-toast';
import { Button, Card, EmptyState, PageHeader, Spinner, Badge } from '../components/ui';
import useNotifications from '../hooks/useNotifications';

function formatWhen(value) {
  if (!value) return '—';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return String(value);
  return d.toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

/**
 * Full-page notifications inbox (all roles). Preferred on mobile.
 */
export default function NotificationsPage() {
  const {
    items,
    unreadCount,
    loading,
    error,
    refreshList,
    markRead,
    markAllRead,
  } = useNotifications({ enabled: true });

  useEffect(() => {
    refreshList({ pageSize: 50 });
  }, [refreshList]);

  const onMarkRead = async (id, isRead) => {
    if (isRead) return;
    try {
      await markRead(id);
    } catch (err) {
      toast.error(err.message || 'Could not mark as read');
    }
  };

  const onMarkAll = async () => {
    try {
      await markAllRead();
      toast.success('All notifications marked as read');
    } catch (err) {
      toast.error(err.message || 'Could not mark all as read');
    }
  };

  return (
    <div className="max-w-2xl mx-auto px-4 sm:px-6 py-6 sm:py-12 w-full min-w-0">
      <PageHeader
        title="Notifications"
        subtitle={unreadCount > 0 ? `${unreadCount} unread` : 'You are up to date.'}
        actions={
          unreadCount > 0 ? (
            <Button variant="secondary" size="sm" className="w-full sm:w-auto" onClick={onMarkAll}>
              Mark all as read
            </Button>
          ) : null
        }
      />

      {loading && items.length === 0 && <Spinner />}

      {!loading && error && (
        <EmptyState message={error} actionLabel="Retry" onAction={() => refreshList({ pageSize: 50 })} />
      )}

      {!loading && !error && items.length === 0 && (
        <EmptyState message="No notifications yet. Appointment updates will appear here." />
      )}

      {items.length > 0 && (
        <ul className="m-0 p-0 list-none space-y-3" data-testid="notifications-list">
          {items.map((n) => (
            <li key={n.id}>
              <Card className={!n.isRead ? 'ring-1 ring-primary/20' : ''}>
                <div className="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-2">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2 mb-1">
                      <h2 className="text-sm font-semibold text-text m-0 break-words">{n.title}</h2>
                      {!n.isRead ? (
                        <Badge status="Pending">Unread</Badge>
                      ) : (
                        <Badge status="Completed">Read</Badge>
                      )}
                    </div>
                    <p className="text-sm text-text m-0 break-words">{n.message}</p>
                    <p className="text-xs text-text-muted m-0 mt-2">{formatWhen(n.createdAt)}</p>
                  </div>
                  {!n.isRead && (
                    <Button
                      size="sm"
                      variant="secondary"
                      className="w-full sm:w-auto shrink-0"
                      onClick={() => onMarkRead(n.id, n.isRead)}
                    >
                      Mark as read
                    </Button>
                  )}
                </div>
              </Card>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
