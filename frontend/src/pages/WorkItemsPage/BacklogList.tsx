import {
    makeStyles,
    tokens,
    Caption1,
    Text,
    Badge,
} from '@fluentui/react-components'
import { BotRegular, PersonRegular } from '@fluentui/react-icons'
import type { WorkItem } from '../../models'
import { PriorityDot } from './PriorityDot'

const STATE_COLORS: Record<string, 'informative' | 'brand' | 'success' | 'warning' | 'danger' | 'important' | 'subtle'> = {
    'New': 'informative',
    'Active': 'brand',
    'In Progress': 'warning',
    'In Progress (AI)': 'warning',
    'Resolved': 'success',
    'Resolved (AI)': 'success',
    'Closed': 'subtle',
}

const PRIORITY_LABELS: Record<number, string> = {
    1: 'P1 — Critical',
    2: 'P2 — High',
    3: 'P3 — Medium',
    4: 'P4 — Low',
}

const useStyles = makeStyles({
    listContainer: {
        flex: 1,
        overflow: 'auto',
        display: 'flex',
        flexDirection: 'column',
        gap: '2px',
    },
    listRow: {
        display: 'grid',
        gridTemplateColumns: '60px 2fr 140px 100px 140px 120px',
        alignItems: 'center',
        padding: '0.5rem 0.75rem',
        borderRadius: tokens.borderRadiusMedium,
        cursor: 'pointer',
        gap: '0.5rem',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },
    listHeader: {
        display: 'grid',
        gridTemplateColumns: '60px 2fr 140px 100px 140px 120px',
        alignItems: 'center',
        padding: '0.5rem 0.75rem',
        gap: '0.5rem',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        marginBottom: '0.25rem',
    },
    cardId: {
        color: tokens.colorBrandForeground1,
        fontWeight: 600,
        fontSize: '12px',
    },
    listTitleText: {
        fontSize: '13px',
    },
    priorityCell: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.25rem',
    },
    assignee: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.25rem',
        color: tokens.colorNeutralForeground3,
        fontSize: '12px',
    },
    tagsRow: {
        display: 'flex',
        gap: '0.25rem',
        flexWrap: 'wrap',
    },
})

interface BacklogListProps {
    items: WorkItem[]
}

export function BacklogList({ items }: BacklogListProps) {
    const styles = useStyles()

    return (
        <div className={styles.listContainer}>
            <div className={styles.listHeader}>
                <Caption1><b>ID</b></Caption1>
                <Caption1><b>Title</b></Caption1>
                <Caption1><b>State</b></Caption1>
                <Caption1><b>Priority</b></Caption1>
                <Caption1><b>Assigned To</b></Caption1>
                <Caption1><b>Tags</b></Caption1>
            </div>
            {items.map((item) => (
                <div key={item.id} className={styles.listRow}>
                    <Text className={styles.cardId}>#{item.id}</Text>
                    <Text className={styles.listTitleText}>{item.title}</Text>
                    <Badge appearance="filled" color={STATE_COLORS[item.state]} size="small">
                        {item.state}
                    </Badge>
                    <div className={styles.priorityCell}>
                        <PriorityDot priority={item.priority} />
                        <Caption1>{PRIORITY_LABELS[item.priority]}</Caption1>
                    </div>
                    <div className={styles.assignee}>
                        {item.isAI ? <BotRegular /> : <PersonRegular />}
                        <Caption1>{item.assignedTo}</Caption1>
                    </div>
                    <div className={styles.tagsRow}>
                        {item.tags.slice(0, 2).map((tag) => (
                            <Badge key={tag} appearance="outline" size="tiny">
                                {tag}
                            </Badge>
                        ))}
                    </div>
                </div>
            ))}
        </div>
    )
}
