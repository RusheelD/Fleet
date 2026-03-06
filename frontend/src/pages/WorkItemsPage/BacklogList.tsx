import { useState, useMemo, useCallback, useRef } from 'react'
import {
    makeStyles,
    tokens,
    Caption1,
    Text,
    Badge,
    Input,
    mergeClasses,
} from '@fluentui/react-components'
import {
    BotRegular,
    PersonRegular,
    ChevronUpRegular,
    ChevronDownRegular,
} from '@fluentui/react-icons'
import type { WorkItem, WorkItemLevel } from '../../models'
import { resolveLevelIcon } from '../../proxies'
import { StateDot } from './StateDot'
import { formatWorkItemState } from './stateLabel'

/* ── Sortable columns ─────────────────────────────────────── */
type SortKey = 'type' | 'title' | 'state' | 'id' | 'difficulty' | 'assignedTo' | 'priority'
type SortDir = 'asc' | 'desc'

const useStyles = makeStyles({
    container: {
        flex: 1,
        overflow: 'auto',
        display: 'flex',
        flexDirection: 'column',
        borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
    },

    /* ── header row ────────────────────────────────────────── */
    header: {
        display: 'grid',
        gridTemplateColumns: '110px 3fr 120px 55px 70px 150px 150px',
        alignItems: 'center',
        paddingTop: tokens.spacingVerticalXS,
        paddingBottom: tokens.spacingVerticalXS,
        paddingLeft: tokens.spacingHorizontalM,
        paddingRight: tokens.spacingHorizontalM,
        gap: tokens.spacingHorizontalS,
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: tokens.colorNeutralBackground2,
        position: 'sticky',
        top: 0,
        zIndex: 1,
        borderLeft: '3px solid transparent',
    },
    headerCell: {
        display: 'flex',
        alignItems: 'center',
        gap: '4px',
        cursor: 'pointer',
        userSelect: 'none',
        ':hover': {
            color: tokens.colorNeutralForeground1,
        },
    },
    headerText: {
        fontSize: '11px',
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground3,
        textTransform: 'uppercase',
        letterSpacing: '0.05em',
    },
    sortIcon: {
        fontSize: '10px',
        color: tokens.colorNeutralForeground3,
    },

    /* ── data rows ─────────────────────────────────────────── */
    row: {
        display: 'grid',
        gridTemplateColumns: '110px 3fr 120px 55px 70px 150px 150px',
        alignItems: 'center',
        paddingTop: '3px',
        paddingBottom: '3px',
        paddingLeft: tokens.spacingHorizontalM,
        paddingRight: tokens.spacingHorizontalM,
        gap: tokens.spacingHorizontalS,
        borderBottom: `1px solid ${tokens.colorNeutralStroke3}`,
        borderLeft: '3px solid transparent',
        cursor: 'pointer',
        minHeight: '34px',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },
    rowSelected: {
        backgroundColor: tokens.colorBrandBackground2,
        ':hover': {
            backgroundColor: tokens.colorBrandBackground2,
        },
    },

    /* ── Work Item Type column ─────────────────────────────── */
    typeCell: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
        minWidth: 0,
    },
    typeIcon: {
        fontSize: '16px',
        display: 'flex',
        alignItems: 'center',
        flexShrink: 0,
    },
    typeName: {
        fontSize: '12px',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        color: tokens.colorNeutralForeground2,
    },
    titleIcon: {
        fontSize: '16px',
        display: 'flex',
        alignItems: 'center',
        flexShrink: 0,
    },

    /* ── Title column ──────────────────────────────────────── */
    titleCell: {
        display: 'flex',
        alignItems: 'center',
        gap: '4px',
        minWidth: 0,
    },
    titleText: {
        fontSize: '13px',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        flexGrow: 1,
        minWidth: 0,
    },
    titleInput: {
        flexGrow: 1,
        minWidth: 0,
    },

    /* ── State column ──────────────────────────────────────── */
    stateCell: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
        minWidth: 0,
    },
    stateText: {
        fontSize: '12px',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },

    /* ── ID column ─────────────────────────────────────────── */
    idText: {
        color: tokens.colorNeutralForeground2,
        fontSize: '12px',
    },

    /* ── Difficulty column ──────────────────────────────────── */
    difficultyText: {
        fontSize: '12px',
        color: tokens.colorNeutralForeground2,
    },

    /* ── Assigned To column ────────────────────────────────── */
    assigneeCell: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
        color: tokens.colorNeutralForeground2,
        fontSize: '12px',
        minWidth: 0,
    },
    assigneeIcon: {
        fontSize: '14px',
        flexShrink: 0,
    },
    assigneeName: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },

    /* ── Tags column ───────────────────────────────────────── */
    tagsCell: {
        display: 'flex',
        gap: '4px',
        flexWrap: 'nowrap',
        overflow: 'hidden',
    },
})

interface BacklogListProps {
    items: WorkItem[]
    levelMap?: Map<number, WorkItemLevel>
    selectedItemId?: number | null
    onItemClick?: (item: WorkItem) => void
    onTitleChange?: (itemId: number, newTitle: string) => void
}

export function BacklogList({ items, levelMap, selectedItemId, onItemClick, onTitleChange }: BacklogListProps) {
    const styles = useStyles()

    /* ── Sorting ───────────────────────────────────────────── */
    const [sortKey, setSortKey] = useState<SortKey>('id')
    const [sortDir, setSortDir] = useState<SortDir>('asc')

    const handleSort = useCallback((key: SortKey) => {
        setSortKey((prev) => {
            if (prev === key) {
                setSortDir((d) => (d === 'asc' ? 'desc' : 'asc'))
                return prev
            }
            setSortDir('asc')
            return key
        })
    }, [])

    const getLevel = useCallback(
        (item: WorkItem): WorkItemLevel | undefined =>
            item.levelId != null ? levelMap?.get(item.levelId) : undefined,
        [levelMap],
    )

    const sortedItems = useMemo(() => {
        const sorted = [...items]
        const dir = sortDir === 'asc' ? 1 : -1
        sorted.sort((a, b) => {
            switch (sortKey) {
                case 'id':
                    return (a.workItemNumber - b.workItemNumber) * dir
                case 'title':
                    return a.title.localeCompare(b.title) * dir
                case 'state':
                    return a.state.localeCompare(b.state) * dir
                case 'type': {
                    const la = getLevel(a)?.name ?? ''
                    const lb = getLevel(b)?.name ?? ''
                    return la.localeCompare(lb) * dir
                }
                case 'assignedTo':
                    return a.assignedTo.localeCompare(b.assignedTo) * dir
                case 'priority':
                    return (a.priority - b.priority) * dir
                case 'difficulty':
                    return (a.difficulty - b.difficulty) * dir
                default:
                    return 0
            }
        })
        return sorted
    }, [items, sortKey, sortDir, getLevel])

    /* ── Inline title editing ──────────────────────────────── */
    const [editingId, setEditingId] = useState<number | null>(null)
    const [editTitle, setEditTitle] = useState('')
    const editInputRef = useRef<HTMLInputElement>(null)

    const startEditing = useCallback((item: WorkItem, e: React.MouseEvent) => {
        e.stopPropagation()
        setEditingId(item.workItemNumber)
        setEditTitle(item.title)
        requestAnimationFrame(() => editInputRef.current?.focus())
    }, [])

    const commitEdit = useCallback(() => {
        if (editingId != null && editTitle.trim() && onTitleChange) {
            const original = items.find((i) => i.workItemNumber === editingId)
            if (original && original.title !== editTitle.trim()) {
                onTitleChange(editingId, editTitle.trim())
            }
        }
        setEditingId(null)
    }, [editingId, editTitle, onTitleChange, items])

    const cancelEdit = useCallback(() => {
        setEditingId(null)
    }, [])

    /* ── Sort indicator ────────────────────────────────────── */
    const SortIndicator = ({ col }: { col: SortKey }) => {
        if (sortKey !== col) return null
        return (
            <span className={styles.sortIcon}>
                {sortDir === 'asc' ? <ChevronUpRegular /> : <ChevronDownRegular />}
            </span>
        )
    }

    return (
        <div className={styles.container}>
            {/* header */}
            <div className={styles.header}>
                <div className={styles.headerCell} onClick={() => handleSort('type')}>
                    <Caption1 className={styles.headerText}>Type</Caption1>
                    <SortIndicator col="type" />
                </div>
                <div className={styles.headerCell} onClick={() => handleSort('title')}>
                    <Caption1 className={styles.headerText}>Title</Caption1>
                    <SortIndicator col="title" />
                </div>
                <div className={styles.headerCell} onClick={() => handleSort('state')}>
                    <Caption1 className={styles.headerText}>State</Caption1>
                    <SortIndicator col="state" />
                </div>
                <div className={styles.headerCell} onClick={() => handleSort('id')}>
                    <Caption1 className={styles.headerText}>ID</Caption1>
                    <SortIndicator col="id" />
                </div>
                <div className={styles.headerCell} onClick={() => handleSort('difficulty')}>
                    <Caption1 className={styles.headerText}>Difficulty</Caption1>
                    <SortIndicator col="difficulty" />
                </div>
                <div className={styles.headerCell} onClick={() => handleSort('assignedTo')}>
                    <Caption1 className={styles.headerText}>Assigned To</Caption1>
                    <SortIndicator col="assignedTo" />
                </div>
                <Caption1 className={styles.headerText}>Tags</Caption1>
            </div>

            {/* rows */}
            {sortedItems.map((item) => {
                const level = getLevel(item)
                const isSelected = item.workItemNumber === selectedItemId
                const isEditing = editingId === item.workItemNumber

                return (
                    <div
                        key={item.workItemNumber}
                        className={mergeClasses(styles.row, isSelected && styles.rowSelected)}
                        style={{ borderLeftColor: level?.color ?? 'transparent' }}
                        onClick={() => {
                            if (!isEditing) onItemClick?.(item)
                        }}
                    >
                        {/* Work Item Type */}
                        <div className={styles.typeCell}>
                            <Text className={styles.typeName}>
                                {level?.name ?? '-'}
                            </Text>
                        </div>

                        {/* Title */}
                        <div className={styles.titleCell}>
                            {level && (
                                <span className={styles.titleIcon} style={{ color: level.color }}>
                                    {resolveLevelIcon(level.iconName)}
                                </span>
                            )}
                            {isEditing ? (
                                <Input
                                    ref={editInputRef}
                                    className={styles.titleInput}
                                    size="small"
                                    appearance="underline"
                                    value={editTitle}
                                    onChange={(_e, data) => setEditTitle(data.value)}
                                    onKeyDown={(e) => {
                                        if (e.key === 'Enter') commitEdit()
                                        if (e.key === 'Escape') cancelEdit()
                                    }}
                                    onBlur={commitEdit}
                                    onClick={(e) => e.stopPropagation()}
                                />
                            ) : (
                                <Text
                                    className={styles.titleText}
                                    onDoubleClick={(e) => startEditing(item, e)}
                                >
                                    {item.title}
                                </Text>
                            )}
                        </div>

                        {/* State */}
                        <div className={styles.stateCell}>
                            <StateDot state={item.state} />
                            <Text className={styles.stateText}>{formatWorkItemState(item.state)}</Text>
                        </div>

                        {/* ID */}
                        <Text className={styles.idText}>{item.workItemNumber}</Text>

                        {/* Difficulty */}
                        <Text className={styles.difficultyText}>
                            {(['', 'D1', 'D2', 'D3', 'D4', 'D5'] as const)[item.difficulty] ?? '-'}
                        </Text>

                        {/* Assigned To */}
                        <div className={styles.assigneeCell}>
                            {item.assignedTo && (
                                <>
                                    <span className={styles.assigneeIcon}>
                                        {item.isAI ? <BotRegular /> : <PersonRegular />}
                                    </span>
                                    <Text className={styles.assigneeName}>{item.assignedTo}</Text>
                                </>
                            )}
                        </div>

                        {/* Tags */}
                        <div className={styles.tagsCell}>
                            {item.tags.slice(0, 3).map((tag) => (
                                <Badge key={tag} appearance="tint" size="tiny" color="informative">
                                    {tag}
                                </Badge>
                            ))}
                        </div>
                    </div>
                )
            })}
        </div>
    )
}

