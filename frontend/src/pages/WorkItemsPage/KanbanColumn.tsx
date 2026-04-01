import {
    makeStyles,
    mergeClasses,
    Title3,
    Text,
} from '@fluentui/react-components'
import { WorkItemCard } from './'
import { usePreferences } from '../../hooks'
import type { WorkItem, WorkItemLevel } from '../../models'
import { appTokens } from '../../styles/appTokens'

const useStyles = makeStyles({
    boardColumn: {
        minWidth: '280px',
        maxWidth: '320px',
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.sm,
    },
    boardColumnCompact: {
        minWidth: '220px',
        maxWidth: '240px',
        gap: appTokens.space.xs,
    },
    boardColumnMobile: {
        minWidth: '100%',
        maxWidth: '100%',
    },
    columnHeader: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        paddingTop: appTokens.space.sm,
        paddingBottom: appTokens.space.sm,
        paddingLeft: appTokens.space.md,
        paddingRight: appTokens.space.md,
        backgroundColor: appTokens.color.pageBackground,
        borderRadius: appTokens.radius.md,
        border: appTokens.border.subtle,
    },
    columnHeaderCompact: {
        paddingTop: appTokens.space.xxs,
        paddingBottom: appTokens.space.xxs,
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
    },
    columnTitle: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.sm,
    },
    columnTitleText: {
        fontSize: appTokens.fontSize.sm,
    },
    columnTitleTextCompact: {
        fontSize: appTokens.fontSize.sm,
        lineHeight: appTokens.lineHeight.snug,
    },
    columnCount: {
        backgroundColor: appTokens.color.surfaceRaised,
        borderRadius: appTokens.radius.full,
        padding: `0 ${appTokens.space.sm}`,
        fontSize: appTokens.fontSize.sm,
        fontWeight: appTokens.fontWeight.semibold,
    },
    cardList: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.sm,
        flex: 1,
        overflow: 'auto',
    },
    cardListCompact: {
        gap: appTokens.space.xs,
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
