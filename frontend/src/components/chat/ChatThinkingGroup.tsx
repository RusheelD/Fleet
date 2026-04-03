import { useMemo, useState } from 'react'
import {
    Caption1,
    Spinner,
    Text,
    makeStyles,
    mergeClasses,
} from '@fluentui/react-components'
import {
    BotRegular,
    ChevronDownRegular,
    ChevronRightRegular,
} from '@fluentui/react-icons'
import { usePreferences } from '../../hooks'
import { appTokens } from '../../styles/appTokens'
import { ChatActivityFeed } from './ChatActivityFeed'
import type { ChatThinkingGroup as ChatThinkingGroupData } from './chatTimeline'
import { formatThinkingDuration } from './chatTimeline'

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xs,
        width: '100%',
        minWidth: 0,
    },
    toggle: {
        width: '100%',
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.sm,
        minWidth: 0,
        paddingTop: appTokens.space.sm,
        paddingBottom: appTokens.space.sm,
        paddingLeft: appTokens.space.md,
        paddingRight: appTokens.space.md,
        borderRadius: appTokens.radius.md,
        border: appTokens.border.subtle,
        backgroundColor: appTokens.color.surfaceRaised,
        color: appTokens.color.textPrimary,
        cursor: 'pointer',
        textAlign: 'left',
        transitionDuration: appTokens.motion.fast,
        transitionProperty: 'background-color, border-color, box-shadow',
        transitionTimingFunction: 'ease',
        ':hover': {
            backgroundColor: appTokens.color.surfaceHover,
        },
    },
    toggleCompact: {
        paddingTop: appTokens.space.xs,
        paddingBottom: appTokens.space.xs,
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
        gap: appTokens.space.xs,
    },
    toggleExpanded: {
        backgroundColor: appTokens.color.surfaceSelected,
        borderTopColor: appTokens.color.brandStroke,
        borderRightColor: appTokens.color.brandStroke,
        borderBottomColor: appTokens.color.brandStroke,
        borderLeftColor: appTokens.color.brandStroke,
    },
    chevron: {
        color: appTokens.color.textTertiary,
        flexShrink: 0,
    },
    stateIcon: {
        color: appTokens.color.brand,
        flexShrink: 0,
    },
    summaryContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxxs,
        minWidth: 0,
        flex: 1,
    },
    summaryRow: {
        display: 'flex',
        alignItems: 'baseline',
        justifyContent: 'space-between',
        gap: appTokens.space.sm,
        minWidth: 0,
    },
    summaryTitle: {
        color: appTokens.color.textPrimary,
        minWidth: 0,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    summaryTime: {
        color: appTokens.color.textTertiary,
        flexShrink: 0,
    },
    summaryDetail: {
        color: appTokens.color.textSecondary,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    detailPanel: {
        paddingLeft: `calc(${appTokens.space.md} + 20px)`,
        minWidth: 0,
    },
    detailPanelCompact: {
        paddingLeft: `calc(${appTokens.space.sm} + 18px)`,
    },
})

interface ChatThinkingGroupProps {
    group: ChatThinkingGroupData
}

export function ChatThinkingGroup({ group }: ChatThinkingGroupProps) {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isCompact = preferences?.compactMode ?? false
    const [expanded, setExpanded] = useState(false)

    const lastActivity = group.activities[group.activities.length - 1]
    const activityCount = group.activities.length

    const summaryTitle = group.state === 'thinking'
        ? 'Fleet AI is thinking...'
        : `Fleet AI thought for ${formatThinkingDuration(group.startedAtUtc, group.endedAtUtc)}`

    const summaryDetail = useMemo(() => {
        const base = `${activityCount} update${activityCount === 1 ? '' : 's'}`
        if (!lastActivity?.message) {
            return base
        }

        return `${base} · ${lastActivity.message}`
    }, [activityCount, lastActivity?.message])

    return (
        <div className={styles.container}>
            <button
                type="button"
                className={mergeClasses(
                    styles.toggle,
                    isCompact && styles.toggleCompact,
                    expanded && styles.toggleExpanded,
                )}
                onClick={() => setExpanded((current) => !current)}
                aria-expanded={expanded}
            >
                {expanded
                    ? <ChevronDownRegular className={styles.chevron} />
                    : <ChevronRightRegular className={styles.chevron} />}
                {group.state === 'thinking'
                    ? <Spinner size="tiny" className={styles.stateIcon} />
                    : <BotRegular className={styles.stateIcon} />}
                <div className={styles.summaryContent}>
                    <div className={styles.summaryRow}>
                        <Text weight="semibold" size={200} className={styles.summaryTitle}>
                            {summaryTitle}
                        </Text>
                        <Caption1 className={styles.summaryTime}>
                            {formatActivityTime(lastActivity?.timestampUtc ?? group.endedAtUtc)}
                        </Caption1>
                    </div>
                    <Caption1 className={styles.summaryDetail}>
                        {summaryDetail}
                    </Caption1>
                </div>
            </button>
            {expanded && (
                <div className={mergeClasses(styles.detailPanel, isCompact && styles.detailPanelCompact)}>
                    <ChatActivityFeed activities={group.activities} />
                </div>
            )}
        </div>
    )
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
