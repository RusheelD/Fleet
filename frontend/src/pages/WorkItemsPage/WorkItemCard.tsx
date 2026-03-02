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
import type { WorkItem } from '../../models'
import { PriorityDot } from './PriorityDot'

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
}

export function WorkItemCard({ item }: WorkItemCardProps) {
    const styles = useStyles()

    return (
        <Card className={styles.workItemCard} size="small">
            <div className={styles.cardTop}>
                <Text className={styles.cardId}>#{item.id}</Text>
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
                <div className={styles.assignee}>
                    {item.isAI ? <BotRegular /> : <PersonRegular />}
                    <Caption1>{item.assignedTo}</Caption1>
                </div>
            </div>
        </Card>
    )
}
