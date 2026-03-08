import {
    makeStyles,
    mergeClasses,
    tokens,
    Title3,
    Text,
} from '@fluentui/react-components'
import { WorkItemCard } from './'
import { usePreferences } from '../../hooks'
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
    boardColumnCompact: {
        minWidth: '220px',
        maxWidth: '240px',
        gap: '0.375rem',
    },
    boardColumnMobile: {
        minWidth: '100%',
        maxWidth: '100%',
    },
    columnHeader: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '0.5rem 0.75rem',
        backgroundColor: tokens.colorNeutralBackground3,
        borderRadius: tokens.borderRadiusMedium,
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
    columnHeaderCompact: {
        paddingTop: '0.25rem',
        paddingBottom: '0.25rem',
        paddingLeft: '0.5rem',
        paddingRight: '0.5rem',
    },
    columnTitle: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
    },
    columnTitleText: {
        fontSize: '14px',
    },
    columnTitleTextCompact: {
        fontSize: '12px',
        lineHeight: '16px',
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
    cardListCompact: {
        gap: '0.375rem',
    },
    cardListMobile: {
        maxHeight: 'unset',
        overflow: 'visible',
    },
})

interface KanbanColumnProps {
    state: string
    items: WorkItem[]
    levelMap?: Map<number, WorkItemLevel>
    onItemClick?: (item: WorkItem) => void
    mobile?: boolean
}

export function KanbanColumn({ state, items, levelMap, onItemClick, mobile = false }: KanbanColumnProps) {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isCompact = preferences?.compactMode ?? false
    const isDense = isCompact || mobile

    return (
        <div className={mergeClasses(styles.boardColumn, isDense && styles.boardColumnCompact, mobile && styles.boardColumnMobile)}>
            <div className={mergeClasses(styles.columnHeader, isDense && styles.columnHeaderCompact)}>
                <div className={styles.columnTitle}>
                    <Title3 className={mergeClasses(styles.columnTitleText, isDense && styles.columnTitleTextCompact)}>
                        {state}
                    </Title3>
                    <Text className={styles.columnCount}>{items.length}</Text>
                </div>
            </div>
            <div className={mergeClasses(styles.cardList, isDense && styles.cardListCompact, mobile && styles.cardListMobile)}>
                {items.map((item) => (
                    <WorkItemCard key={item.workItemNumber} item={item} levelMap={levelMap} onItemClick={onItemClick} />
                ))}
            </div>
        </div>
    )
}
