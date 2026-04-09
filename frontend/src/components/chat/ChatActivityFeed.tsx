import {
    Caption1,
    Text,
    makeStyles,
    mergeClasses,
} from '@fluentui/react-components'
import {
    DismissCircleRegular,
    InfoRegular,
    WarningRegular,
    WrenchRegular,
} from '@fluentui/react-icons'
import { appTokens } from '../../styles/appTokens'
import { normalizeChatSessionActivity, type ChatSessionActivity } from '../../models/chat'
import { usePreferences } from '../../hooks/PreferencesContext'

const useStyles = makeStyles({
    feed: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.sm,
        paddingTop: appTokens.space.xs,
        paddingBottom: appTokens.space.xs,
    },
    feedCompact: {
        gap: appTokens.space.xs,
    },
    item: {
        display: 'flex',
        gap: appTokens.space.sm,
        alignItems: 'flex-start',
        paddingTop: appTokens.space.sm,
        paddingBottom: appTokens.space.sm,
        paddingLeft: appTokens.space.md,
        paddingRight: appTokens.space.md,
        borderRadius: appTokens.radius.md,
        backgroundColor: appTokens.color.surfaceRaised,
        border: appTokens.border.subtle,
    },
    itemCompact: {
        paddingTop: appTokens.space.xs,
        paddingBottom: appTokens.space.xs,
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
    },
    icon: {
        color: appTokens.color.info,
        flexShrink: 0,
        marginTop: '2px',
    },
    iconStatus: {
        color: appTokens.color.brand,
    },
    iconTool: {
        color: appTokens.color.brand,
    },
    iconWarning: {
        color: appTokens.color.warning,
    },
    iconDanger: {
        color: appTokens.color.danger,
    },
    content: {
        display: 'flex',
        flexDirection: 'column',
        minWidth: 0,
        gap: appTokens.space.xxxs,
        flex: 1,
    },
    titleRow: {
        display: 'flex',
        gap: appTokens.space.sm,
        alignItems: 'baseline',
        justifyContent: 'space-between',
        minWidth: 0,
    },
    title: {
        color: appTokens.color.textPrimary,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    meta: {
        color: appTokens.color.textTertiary,
        flexShrink: 0,
    },
    message: {
        color: appTokens.color.textSecondary,
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
    },
})

interface ChatActivityFeedProps {
    activities: ChatSessionActivity[]
}

export function ChatActivityFeed({ activities }: ChatActivityFeedProps) {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isCompact = preferences?.compactMode ?? false

    if (activities.length === 0) {
        return null
    }

    return (
        <div className={mergeClasses(styles.feed, isCompact && styles.feedCompact)}>
            {activities.map((activity, index) => {
                const normalizedActivity = normalizeChatSessionActivity(activity, index)
                return (
                    <div
                        key={normalizedActivity.id}
                        className={mergeClasses(styles.item, isCompact && styles.itemCompact)}
                    >
                        {renderActivityIcon(normalizedActivity, styles)}
                        <div className={styles.content}>
                            <div className={styles.titleRow}>
                                <Text weight="semibold" size={200} className={styles.title}>
                                    {getActivityTitle(normalizedActivity)}
                                </Text>
                                <Caption1 className={styles.meta}>
                                    {formatActivityTime(normalizedActivity.timestampUtc)}
                                </Caption1>
                            </div>
                            <Caption1 className={styles.message}>
                                {normalizedActivity.message}
                            </Caption1>
                        </div>
                    </div>
                )
            })}
        </div>
    )
}

function renderActivityIcon(
    activity: ChatSessionActivity,
    styles: ReturnType<typeof useStyles>,
) {
    const normalizedMessage = typeof activity.message === 'string'
        ? activity.message.toLowerCase()
        : ''

    if (activity.kind === 'tool') {
        return <WrenchRegular className={mergeClasses(styles.icon, styles.iconTool)} />
    }

    if (activity.kind === 'error' || activity.succeeded === false) {
        return <DismissCircleRegular className={mergeClasses(styles.icon, styles.iconDanger)} />
    }

    if (normalizedMessage.includes('cancel')
        || normalizedMessage.includes('interrupt')) {
        return <WarningRegular className={mergeClasses(styles.icon, styles.iconWarning)} />
    }

    return <InfoRegular className={mergeClasses(styles.icon, styles.iconStatus)} />
}

function getActivityTitle(activity: ChatSessionActivity): string {
    if (activity.kind === 'tool' && activity.toolName) {
        return activity.toolName.replace(/_/g, ' ')
    }

    if (activity.kind === 'error') {
        return 'Generation issue'
    }

    return 'Session update'
}

function formatActivityTime(timestampUtc: string): string {
    const date = new Date(timestampUtc)
    if (Number.isNaN(date.getTime())) {
        return ''
    }

    return date.toLocaleTimeString([], {
        hour: 'numeric',
        minute: '2-digit',
    })
}
