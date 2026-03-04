import {
    makeStyles,
    tokens,
    Title3,
    Text,
    Button,
} from '@fluentui/react-components'
import { MoreHorizontalRegular } from '@fluentui/react-icons'
import { WorkItemCard } from './'
import type { WorkItem, WorkItemLevel } from '../../models'

const useStyles = makeStyles({
    boardColumn: {
        minWidth: '280px',
        maxWidth: '320px',
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        gap: '0.5rem',
    },
    columnHeader: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '0.5rem 0.75rem',
        backgroundColor: tokens.colorNeutralBackground3,
        borderRadius: tokens.borderRadiusMedium,
    },
    columnTitle: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
    },
    columnTitleText: {
        fontSize: '14px',
    },
    columnCount: {
        backgroundColor: tokens.colorNeutralBackground5,
        borderRadius: '99px',
        padding: '0 0.5rem',
        fontSize: '12px',
        fontWeight: 600,
    },
    cardList: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0.5rem',
        flex: 1,
        overflow: 'auto',
    },
})

interface KanbanColumnProps {
    state: string
    items: WorkItem[]
    levelMap?: Map<number, WorkItemLevel>
    onItemClick?: (item: WorkItem) => void
}

export function KanbanColumn({ state, items, levelMap, onItemClick }: KanbanColumnProps) {
    const styles = useStyles()

    return (
        <div className={styles.boardColumn}>
            <div className={styles.columnHeader}>
                <div className={styles.columnTitle}>
                    <Title3 className={styles.columnTitleText}>{state}</Title3>
                    <Text className={styles.columnCount}>{items.length}</Text>
                </div>
                <Button
                    appearance="subtle"
                    size="small"
                    icon={<MoreHorizontalRegular />}
                    aria-label="Column options"
                />
            </div>
            <div className={styles.cardList}>
                {items.map((item) => (
                    <WorkItemCard key={item.workItemNumber} item={item} levelMap={levelMap} onItemClick={onItemClick} />
                ))}
            </div>
        </div>
    )
}
