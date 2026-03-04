import {
    makeStyles,
    tokens,
    Caption1,
    Text,
    Card,
    Badge,
} from '@fluentui/react-components'
import {
    BotRegular,
    PersonRegular,
    TagRegular,
} from '@fluentui/react-icons'
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
    },
})

interface WorkItemCardProps {
    item: WorkItem
    levelMap?: Map<number, WorkItemLevel>
    onItemClick?: (item: WorkItem) => void
}

export function WorkItemCard({ item, levelMap, onItemClick }: WorkItemCardProps) {
    const styles = useStyles()
    const level = item.levelId != null ? levelMap?.get(item.levelId) : undefined

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
