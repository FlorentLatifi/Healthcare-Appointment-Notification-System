import apiClient from './apiClient';

/**
 * Fetch paged notifications for the current user.
 * @returns {Promise<{ items: any[], unreadCount: number, totalCount: number, pageNumber: number, totalPages: number }>}
 */
export async function fetchNotifications({ pageNumber = 1, pageSize = 20 } = {}) {
  const { data } = await apiClient.get('/Notifications', {
    params: { pageNumber, pageSize },
  });
  if (!data?.success) {
    throw new Error(data?.message || 'Failed to load notifications');
  }
  return data.data || { items: [], unreadCount: 0, totalCount: 0, pageNumber: 1, totalPages: 1 };
}

export async function fetchUnreadCount() {
  const { data } = await apiClient.get('/Notifications/unread-count');
  if (!data?.success) {
    throw new Error(data?.message || 'Failed to load unread count');
  }
  return Number(data.data?.count ?? 0);
}

export async function markNotificationRead(id) {
  const { data } = await apiClient.put(`/Notifications/${id}/read`);
  if (!data?.success) {
    throw new Error(data?.message || 'Failed to mark as read');
  }
  return true;
}

export async function markAllNotificationsRead() {
  const { data } = await apiClient.put('/Notifications/read-all');
  if (!data?.success) {
    throw new Error(data?.message || 'Failed to mark all as read');
  }
  return true;
}
