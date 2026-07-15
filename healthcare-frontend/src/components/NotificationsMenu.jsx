import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { Bell } from 'lucide-react';
import { Button, EmptyState, Spinner } from './ui';
import useNotifications from '../hooks/useNotifications';

function formatWhen(value) {
  if (!value) return '';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return String(value);
  return d.toLocaleString(undefined, {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

/**
 * Navbar notifications control:
 * - Desktop: dropdown panel
 * - Mobile: navigates to /notifications full page (also available from "View all")
 */
export default function NotificationsMenu({ enabled }) {
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const panelRef = useRef(null);
  const {
    items,
    unreadCount,
    loading,
    refreshList,
    markRead,
    markAllRead,
  } = useNotifications({ enabled });

  useEffect(() => {
    if (!open) return undefined;
    refreshList({ pageSize: 8 });
    const onDoc = (e) => {
      if (panelRef.current && !panelRef.current.contains(e.target)) {
        setOpen(false);
      }
    };
    const onKey = (e) => {
      if (e.key === 'Escape') setOpen(false);
    };
    document.addEventListener('mousedown', onDoc);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onDoc);
      document.removeEventListener('keydown', onKey);
    };
  }, [open, refreshList]);

  if (!enabled) return null;

  const badge = unreadCount > 0 ? (unreadCount > 99 ? '99+' : String(unreadCount)) : null;

  const handleToggle = () => {
    // On very small screens go to full page for readability.
    if (typeof window !== 'undefined' && window.matchMedia('(max-width: 639px)').matches) {
      navigate('/notifications');
      return;
    }
    setOpen((v) => !v);
  };

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
    <div className="relative shrink-0" ref={panelRef}>
      <button
        type="button"
        className="relative inline-flex items-center justify-center min-w-11 min-h-11 p-2 rounded-md text-text-muted hover:text-text hover:bg-surface cursor-pointer bg-transparent border-none focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
        aria-label={badge ? `Notifications, ${badge} unread` : 'Notifications'}
        aria-expanded={open}
        aria-haspopup="true"
        onClick={handleToggle}
        data-testid="notifications-bell"
      >
        <Bell size={18} aria-hidden="true" />
        {badge && (
          <span
            className="absolute top-1.5 right-1.5 min-w-[1.1rem] h-[1.1rem] px-1 rounded-full bg-status-cancelled-text text-white text-[10px] font-semibold leading-[1.1rem] text-center"
            data-testid="notifications-badge"
          >
            {badge}
          </span>
        )}
      </button>

      {open && (
        <div
          className="absolute right-0 mt-2 w-[min(22rem,calc(100vw-1.5rem))] max-h-[min(28rem,70vh)] overflow-hidden rounded-xl border border-border-light bg-white shadow-elevated z-50 flex flex-col"
          role="dialog"
          aria-label="Notifications"
          data-testid="notifications-dropdown"
        >
          <div className="flex items-center justify-between gap-2 px-3 py-2.5 border-b border-border-light">
            <p className="text-sm font-semibold text-text m-0">Notifications</p>
            <div className="flex items-center gap-1">
              {unreadCount > 0 && (
                <Button variant="ghost" size="sm" onClick={onMarkAll}>
                  Mark all read
                </Button>
              )}
              <Button
                variant="ghost"
                size="sm"
                onClick={() => {
                  setOpen(false);
                  navigate('/notifications');
                }}
              >
                View all
              </Button>
            </div>
          </div>

          <div className="overflow-y-auto flex-1 min-h-0">
            {loading && items.length === 0 ? (
              <div className="p-6"><Spinner /></div>
            ) : items.length === 0 ? (
              <div className="p-4">
                <EmptyState message="No notifications yet." />
              </div>
            ) : (
              <ul className="m-0 p-0 list-none divide-y divide-border-light">
                {items.map((n) => (
                  <li key={n.id}>
                    <button
                      type="button"
                      className={`w-full text-left px-3 py-2.5 bg-transparent border-none cursor-pointer hover:bg-surface focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-primary ${
                        n.isRead ? 'opacity-80' : 'bg-surface/50'
                      }`}
                      onClick={() => onMarkRead(n.id, n.isRead)}
                    >
                      <div className="flex items-start gap-2">
                        {!n.isRead && (
                          <span className="mt-1.5 w-2 h-2 rounded-full bg-primary shrink-0" aria-hidden="true" />
                        )}
                        <div className={`min-w-0 flex-1 ${n.isRead ? '' : 'pl-0'}`}>
                          <p className={`text-sm m-0 break-words ${n.isRead ? 'font-medium text-text' : 'font-semibold text-text'}`}>
                            {n.title}
                          </p>
                          <p className="text-xs text-text-muted m-0 mt-0.5 break-words line-clamp-2">{n.message}</p>
                          <p className="text-[11px] text-text-light m-0 mt-1">{formatWhen(n.createdAt)}</p>
                        </div>
                      </div>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
