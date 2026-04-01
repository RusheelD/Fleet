import {
    makeStyles,
    Caption1,
    Text,
    Card,
    Badge,
    mergeClasses,
} from '@fluentui/react-components'
import {
    BotRegular,
    PersonRegular,
    TagRegular,
} from '@fluentui/react-icons'
import { usePreferences, useIsMobile } from '../../hooks'
import { appTokens } from '../../styles/appTokens'
import type { WorkItem, WorkItemLevel } from '../../models'
import { PriorityDot } from './'
import { LevelBadge } from '../../components/shared'

const useStyles = makeStyles({
    workItemCard: {
        padding: '0.75rem',
        display: 'flex',
        flexDirection: 'column',
        gap: '0.5rem',
        cursor: 'pointer',
        ':hover': {
            boxShadow: appTokens.shadow.card,
        },
    },
    workItemCardCompact: {
        paddingTop: '0.375rem',
        paddingBottom: '0.375rem',
        paddingLeft: '0.5rem',
        paddingRight: '0.5rem',
        gap: '0.25rem',
        borderTopWidth: '1px',
        borderRightWidth: '1px',
        borderBottomWidth: '1px',
        borderLeftWidth: '1px',
        borderTopStyle: 'solid',
        borderRightStyle: 'solid',
        borderBottomStyle: 'solid',
        borderLeftStyle: 'solid',
        borderTopColor: appTokens.color.border,
        borderRightColor: appTokens.color.border,
        borderBottomColor: appTokens.color.border,
        borderLeftColor: appTokens.color.border,
    },
    compactTopRow: {
        display: 'grid',
        gridTemplateColumns: 'auto 1fr auto',
        alignItems: 'center',
        gap: '0.375rem',
        minWidth: 0,
    },
    compactBottomRow: {
        display: 'grid',
        gridTemplateColumns: 'auto 1fr auto',
        alignItems: 'center',
        gap: '0.375rem',
        minWidth: 0,
    },
    compactBottomRowMobile: {
        gridTemplateColumns: '1fr',
        alignItems: 'flex-start',
    },
    cardTop: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
    },
    cardId: {
        color: appTokens.color.brand,
        fontWeight: 600,
        fontSize: '12px',
    },
    cardTitle: {
        fontWeight: 600,
        fontSize: '13px',
        lineHeight: '18px',
    },
    cardTitleCompact: {
        fontWeight: 600,
        fontSize: '12px',
        lineHeight: '16px',
        whiteSpace: 'normal',
        overflow: 'visible',
        textOverflow: 'clip',
    },
    cardTitleMobile: {
        display: '-webkit-box',
        WebkitLineClamp: '2',
        WebkitBoxOrient: 'vertical',
        overflow: 'hidden',
    },
    cardFooter: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: '0.25rem',
    },
    tagsRow: {
        display: 'flex',
        gap: '0.25rem',
        flexWrap: 'wrap',
        minWidth: 0,
    },
    tagBadge: {
        display: 'inline-flex',
        alignItems: 'center',
        maxWidth: '11rem',
        minWidth: 0,
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        wordBreak: 'normal',
        overflowWrap: 'normal',
    },
    assignee: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.25rem',
        color: appTokens.color.textTertiary,
        fontSize: '12px',
        minWidth: 0,
    },
    compactAssignee: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        maxWidth: '120px',
    },
    compactAssigneeMobile: {
        maxWidth: 'none',
        whiteSpace: 'normal',
        overflow: 'visible',
        textOverflow: 'clip',
    },
})

interface WorkItemCardProps {
    item: WorkItem
    levelMap?: Map<number, WorkItemLevel>
    onItemClick?: (item: WorkItem) => void
}

export function WorkItemCard({ item, levelMap, onItemClick }: WorkItemCardProps) {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isMobile = useIsMobile()
    const isCompact = preferences?.compactMode ?? false
    const isDense = isCompact || isMobile
    const level = item.levelId != null ? levelMap?.get(item.levelId) : undefined

    if (isDense) {
        return (
            <Card
                className={mergeClasses(styles.workItemCard, styles.workItemCardCompact)}
                size="small"
                onClick={() => onItemClick?.(item)}
            >
                <div className={styles.compactTopRow}>
                    <Text className={styles.cardId}>#{item.workItemNumber}</Text>
                    <Text className={mergeClasses(styles.cardTitleCompact, isMobile && styles.cardTitleMobile)}>{item.title}</Text>
                    <PriorityDot priority={item.priority} />
                </div>
                <div className={mergeClasses(styles.compactBottomRow, isMobile && styles.compactBottomRowMobile)}>
                    <div className={styles.tagsRow}>
                        <LevelBadge level={level} />
                        {item.tags.slice(0, 1).map((tag) => (
                            <Badge key={tag} appearance="outline" size="small" icon={<TagRegular />} className={styles.tagBadge}>
                                {tag}
                            </Badge>
                        ))}
                    </div>
                    <div />
                    {item.assignedTo && (
                        <div className={styles.assignee}>
                            {item.isAI ? <BotRegular /> : <PersonRegular />}
                            <Caption1 className={mergeClasses(styles.compactAssignee, isMobile && styles.compactAssigneeMobile)}>
                                {item.assignedTo}
                            </Caption1>
                        </div>
                    )}
                </div>
            </Card>
        )
    }

    return (
        <Card className={styles.workItemCard} size="small" onClick={() => onItemClick?.(item)}>
            <div className={styles.cardTop}>
                <div className={styles.tagsRow}>
                    <Text className={styles.cardId}>#{item.workItemNumber}</Text>
                    <LevelBadge level={level} />
                </div>
                <PriorityDot priority={item.priority} />
            </div>
            <Text className={styles.cardTitle}>{item.title}</Text>
            <div className={styles.cardFooter}>
                <div className={styles.tagsRow}>
                    {item.tags.slice(0, 2).map((tag) => (
                        <Badge key={tag} appearance="outline" size="small" icon={<TagRegular />} className={styles.tagBadge}>
                            {tag}
                        </Badge>
                    ))}
                </div>
                {item.assignedTo && (
                    <div className={styles.assignee}>
                        {item.isAI ? <BotRegular /> : <PersonRegular />}
                        <Caption1>{item.assignedTo}</Caption1>
                    </div>
                )}
            </div>
        </Card>
    )
}
