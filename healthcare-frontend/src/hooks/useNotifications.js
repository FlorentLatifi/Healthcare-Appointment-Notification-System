import { useCallback, useEffect, useState } from 'react';
import {
  fetchNotifications,
  fetchUnreadCount,
  markAllNotificationsRead,
  markNotificationRead,
} from '../services/notificationsApi';

/**
 * Shared notifications state for navbar badge + list views.
 */
export default function useNotifications({ enabled = true, pollMs = 60000 } = {}) {
  const [items, setItems] = useState([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const refreshCount = useCallback(async () => {
    if (!enabled) return;
    try {
      const count = await fetchUnreadCount();
      setUnreadCount(count);
    } catch {
      // badge is non-critical
    }
  }, [enabled]);

  const refreshList = useCallback(async ({ pageNumber = 1, pageSize = 30 } = {}) => {
    if (!enabled) return;
    setLoading(true);
    setError(null);
    try {
      const data = await fetchNotifications({ pageNumber, pageSize });
      setItems(Array.isArray(data.items) ? data.items : []);
      if (typeof data.unreadCount === 'number') setUnreadCount(data.unreadCount);
    } catch (err) {
      setError(err.message || 'Failed to load notifications');
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, [enabled]);

  const markRead = useCallback(async (id) => {
    await markNotificationRead(id);
    setItems((prev) => prev.map((n) => (n.id === id ? { ...n, isRead: true } : n)));
    setUnreadCount((c) => Math.max(0, c - 1));
  }, []);

  const markAllRead = useCallback(async () => {
    await markAllNotificationsRead();
    setItems((prev) => prev.map((n) => ({ ...n, isRead: true })));
    setUnreadCount(0);
  }, []);

  useEffect(() => {
    if (!enabled) {
      setItems([]);
      setUnreadCount(0);
      return undefined;
    }
    refreshCount();
    const id = setInterval(refreshCount, pollMs);
    return () => clearInterval(id);
  }, [enabled, pollMs, refreshCount]);

  return {
    items,
    unreadCount,
    loading,
    error,
    refreshCount,
    refreshList,
    markRead,
    markAllRead,
  };
}
