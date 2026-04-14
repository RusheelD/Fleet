import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  AlertRegular,
  ArrowReplyRegular,
  CheckmarkRegular,
} from '@fluentui/react-icons'
import {
  Badge,
  Button,
  Card,
  Caption1,
  Divider,
  Spinner,
  Tab,
  TabList,
  Text,
  makeStyles,
  mergeClasses,
} from '@fluentui/react-components'
import { EmptyState, PageShell } from '../../components/shared'
import { NotificationsTab } from '../SettingsPage/NotificationsTab'
import {
  useMarkAllNotificationsAsRead,
  useMarkNotificationAsRead,
  useNotifications,
  useProjects,
} from '../../proxies'
import { useIsMobile } from '../../hooks'
import { appTokens } from '../../styles/appTokens'
import type { NotificationEvent } from '../../models'

const useStyles = makeStyles({
  summaryGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))',
    gap: appTokens.space.md,
  },
  summaryCard: {
    padding: appTokens.space.lg,
    display: 'flex',
    flexDirection: 'column',
    gap: appTokens.space.xs,
    backgroundColor: appTokens.color.surface,
  },
  summaryEyebrow: {
    color: appTokens.color.textMuted,
    textTransform: 'uppercase',
    letterSpacing: '0.06em',
  },
  summaryValue: {
    fontSize: appTokens.fontSize.metric,
    lineHeight: appTokens.lineHeight.tight,
    fontWeight: appTokens.fontWeight.semibold,
    color: appTokens.color.textPrimary,
  },
  inboxCard: {
    padding: appTokens.space.lg,
    display: 'flex',
    flexDirection: 'column',
    gap: appTokens.space.lg,
  },
  inboxCardMobile: {
    paddingTop: appTokens.space.md,
    paddingBottom: appTokens.space.md,
    paddingLeft: appTokens.space.sm,
    paddingRight: appTokens.space.sm,
    gap: appTokens.space.md,
  },
  inboxHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    gap: appTokens.space.md,
    flexWrap: 'wrap',
  },
  inboxTitleBlock: {
    display: 'flex',
    flexDirection: 'column',
    gap: appTokens.space.xxs,
  },
  inboxActions: {
    display: 'none',
  },
  filterTabs: {
    marginTop: appTokens.space.xxs,
  },
  notificationList: {
    display: 'flex',
    flexDirection: 'column',
    gap: appTokens.space.md,
  },
  notificationCard: {
    padding: appTokens.space.md,
    display: 'flex',
    flexDirection: 'column',
    gap: appTokens.space.sm,
    backgroundColor: appTokens.color.pageBackground,
    border: appTokens.border.subtle,
  },
  notificationCardUnread: {
    boxShadow: appTokens.border.activeInset,
    backgroundColor: appTokens.color.surfaceBrand,
  },
  notificationCardHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    gap: appTokens.space.md,
    flexWrap: 'wrap',
  },
  notificationMeta: {
    display: 'flex',
    flexDirection: 'column',
    gap: appTokens.space.xxs,
    minWidth: 0,
  },
  notificationTitleRow: {
    display: 'flex',
    alignItems: 'center',
    gap: appTokens.space.xs,
    flexWrap: 'wrap',
  },
  notificationBody: {
    color: appTokens.color.textSecondary,
    whiteSpace: 'pre-wrap',
  },
  notificationMetaRow: {
    display: 'flex',
    gap: appTokens.space.xs,
    flexWrap: 'wrap',
    alignItems: 'center',
  },
  notificationActions: {
    display: 'flex',
    gap: appTokens.space.sm,
    flexWrap: 'wrap',
  },
  notificationActionsMobile: {
    width: '100%',
    display: 'grid',
    gridTemplateColumns: '1fr',
  },
})

function formatNotificationType(type: string) {
  return type
    .replace(/[_-]+/g, ' ')
    .replace(/\b\w/g, (character) => character.toUpperCase())
}

function formatTimestamp(timestamp: string) {
  return new Date(timestamp).toLocaleString()
}

export function NotificationsPage() {
  const styles = useStyles()
  const navigate = useNavigate()
  const isMobile = useIsMobile()
  const [view, setView] = useState<'unread' | 'all'>('unread')
  const unreadNotifications = useNotifications(true)
  const allNotifications = useNotifications(false)
  const projects = useProjects()
  const markNotificationAsRead = useMarkNotificationAsRead()
  const markAllNotificationsAsRead = useMarkAllNotificationsAsRead()

  const unreadCount = unreadNotifications.data?.length ?? 0
  const totalCount = allNotifications.data?.length ?? 0
  const activeNotifications = view === 'unread'
    ? unreadNotifications.data ?? []
    : allNotifications.data ?? []
  const isLoading = view === 'unread'
    ? unreadNotifications.isLoading
    : allNotifications.isLoading

  const projectLookup = useMemo(
    () => new Map((projects.data ?? []).map((project) => [project.id, project])),
    [projects.data],
  )

  const openNotificationContext = async (notification: NotificationEvent) => {
    if (!notification.isRead) {
      try {
        await markNotificationAsRead.mutateAsync(notification.id)
      } catch {
        // Navigate even if the read acknowledgement fails.
      }
    }

    const project = projectLookup.get(notification.projectId)
    if (!project) {
      navigate('/projects')
      return
    }

    navigate(notification.executionId ? `/projects/${project.slug}/agents` : `/projects/${project.slug}`)
  }

  return (
    <PageShell
      title="Notifications"
      subtitle="Execution updates, pull request activity, and project events."
      maxWidth="large"
      actions={unreadCount > 0 ? (
        <Button
          appearance="primary"
          icon={<CheckmarkRegular />}
          onClick={() => markAllNotificationsAsRead.mutate()}
          disabled={markAllNotificationsAsRead.isPending}
        >
          {markAllNotificationsAsRead.isPending ? 'Clearing...' : 'Mark All Read'}
        </Button>
      ) : undefined}
    >
      <div className={styles.summaryGrid}>
        <Card className={styles.summaryCard}>
          <Caption1 className={styles.summaryEyebrow}>Unread</Caption1>
          <Text className={styles.summaryValue}>{unreadCount}</Text>
          <Caption1>Needs attention right now</Caption1>
        </Card>
        <Card className={styles.summaryCard}>
          <Caption1 className={styles.summaryEyebrow}>Total</Caption1>
          <Text className={styles.summaryValue}>{totalCount}</Text>
          <Caption1>Events currently stored in your inbox</Caption1>
        </Card>
      </div>

      <Card className={mergeClasses(styles.inboxCard, isMobile && styles.inboxCardMobile)}>
        <div className={styles.inboxHeader}>
          <div className={styles.inboxTitleBlock}>
            <Text weight="semibold" size={500}>Inbox</Text>
            <Caption1>Open a notification to jump back into the relevant project context.</Caption1>
            <TabList
              selectedValue={view}
              onTabSelect={(_event, data) => setView(data.value as 'unread' | 'all')}
              className={styles.filterTabs}
              size="small"
            >
              <Tab value="unread">Unread</Tab>
              <Tab value="all">All</Tab>
            </TabList>
          </div>
        </div>

        <Divider />

        {isLoading ? (
          <Spinner label="Loading notifications..." />
        ) : activeNotifications.length === 0 ? (
          <EmptyState
            icon={<AlertRegular fontSize={32} />}
            title={view === 'unread' ? 'No unread notifications' : 'No notifications yet'}
            description={view === 'unread'
              ? 'You are caught up. New execution and project events will appear here.'
              : 'Fleet has not generated any inbox events yet.'}
          />
        ) : (
          <div className={styles.notificationList}>
            {activeNotifications.map((notification) => {
              const project = projectLookup.get(notification.projectId)
              const projectLabel = project?.title ?? 'Project unavailable'

              return (
                <Card
                  key={notification.id}
                  className={mergeClasses(
                    styles.notificationCard,
                    !notification.isRead && styles.notificationCardUnread,
                  )}
                >
                  <div className={styles.notificationCardHeader}>
                    <div className={styles.notificationMeta}>
                      <div className={styles.notificationTitleRow}>
                        <Text weight="semibold">{notification.title}</Text>
                        {!notification.isRead && <Badge color="danger">Unread</Badge>}
                        <Badge appearance="outline">{formatNotificationType(notification.type)}</Badge>
                      </div>
                      <Caption1>{formatTimestamp(notification.createdAtUtc)}</Caption1>
                    </div>

                    <div className={mergeClasses(styles.notificationActions, isMobile && styles.notificationActionsMobile)}>
                      <Button
                        appearance="primary"
                        size="small"
                        icon={<ArrowReplyRegular />}
                        onClick={() => void openNotificationContext(notification)}
                      >
                        Open Context
                      </Button>
                      {!notification.isRead && (
                        <Button
                          appearance="secondary"
                          size="small"
                          icon={<CheckmarkRegular />}
                          onClick={() => markNotificationAsRead.mutate(notification.id)}
                          disabled={markNotificationAsRead.isPending}
                        >
                          Mark Read
                        </Button>
                      )}
                    </div>
                  </div>

                  <Text size={300} className={styles.notificationBody}>{notification.message}</Text>

                  <div className={styles.notificationMetaRow}>
                    <Badge appearance="outline">{projectLabel}</Badge>
                    {notification.executionId ? (
                      <Badge appearance="outline">Execution {notification.executionId}</Badge>
                    ) : null}
                  </div>
                </Card>
              )
            })}
          </div>
        )}
      </Card>

      <NotificationsTab />
    </PageShell>
  )
}
