import { get, post } from './'
import type { NotificationEvent } from '../models'

export function getNotifications(unreadOnly = false): Promise<NotificationEvent[]> {
  return get<NotificationEvent[]>(`/api/notifications?unreadOnly=${unreadOnly}`)
}

export function markNotificationAsRead(notificationId: number): Promise<void> {
  return post<void>(`/api/notifications/${notificationId}/read`, {})
}

export function markAllNotificationsAsRead(): Promise<void> {
  return post<void>('/api/notifications/read-all', {})
}
