import {
    makeStyles,
    tokens,
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
import { usePreferences } from '../../hooks'
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
            boxShadow: tokens.shadow4,
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
        borderTopColor: tokens.colorNeutralStroke2,
        borderRightColor: tokens.colorNeutralStroke2,
        borderBottomColor: tokens.colorNeutralStroke2,
        borderLeftColor: tokens.colorNeutralStroke2,
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
    cardTop: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
    },
    cardId: {
        color: tokens.colorBrandForeground1,
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
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
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
    },
    assignee: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.25rem',
        color: tokens.colorNeutralForeground3,
        fontSize: '12px',
        minWidth: 0,
    },
    compactAssignee: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        maxWidth: '120px',
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
    const isCompact = preferences?.compactMode ?? false
    const level = item.levelId != null ? levelMap?.get(item.levelId) : undefined

    if (isCompact) {
        return (
            <Card
                className={mergeClasses(styles.workItemCard, styles.workItemCardCompact)}
                size="small"
                onClick={() => onItemClick?.(item)}
            >
                <div className={styles.compactTopRow}>
                    <Text className={styles.cardId}>#{item.workItemNumber}</Text>
                    <Text className={styles.cardTitleCompact}>{item.title}</Text>
                    <PriorityDot priority={item.priority} />
                </div>
                <div className={styles.compactBottomRow}>
                    <div className={styles.tagsRow}>
                        <LevelBadge level={level} />
                        {item.tags.slice(0, 1).map((tag) => (
                            <Badge key={tag} appearance="outline" size="small" icon={<TagRegular />}>
                                {tag}
                            </Badge>
                        ))}
                    </div>
                    <div />
                    {item.assignedTo && (
                        <div className={styles.assignee}>
                            {item.isAI ? <BotRegular /> : <PersonRegular />}
                            <Caption1 className={styles.compactAssignee}>{item.assignedTo}</Caption1>
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
                        <Badge key={tag} appearance="outline" size="small" icon={<TagRegular />}>
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
