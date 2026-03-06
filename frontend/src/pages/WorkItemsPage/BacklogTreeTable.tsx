import { useState, useMemo, useCallback, useRef, useEffect } from 'react'
import {
    makeStyles,
    tokens,
    Caption1,
    Text,
    Badge,
    Button,
    Input,
    mergeClasses,
} from '@fluentui/react-components'
import {
    ChevronRightRegular,
    ChevronDownRegular,
    BotRegular,
    PersonRegular,
    ReOrderRegular,
} from '@fluentui/react-icons'
import type { WorkItem, WorkItemLevel } from '../../models'
import { resolveLevelIcon } from '../../proxies'
import { StateDot } from './StateDot'
import { formatWorkItemState } from './stateLabel'

/* ── Drop zone enum ────────────────────────────────────────── */
type DropZone = 'above' | 'on' | 'below' | null

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
    headerText: {
        fontSize: '11px',
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground3,
        textTransform: 'uppercase',
        letterSpacing: '0.05em',
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
        position: 'relative',
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
    rowDragging: {
        opacity: 0.4,
    },

    /* ── Drop indicators ───────────────────────────────────── */
    dropAbove: {
        '::before': {
            content: '""',
            position: 'absolute',
            top: '-1px',
            left: 0,
            right: 0,
            height: '2px',
            backgroundColor: tokens.colorBrandForeground1,
            zIndex: 2,
        },
    },
    dropOn: {
        backgroundColor: tokens.colorBrandBackground2,
        outline: `2px solid ${tokens.colorBrandForeground1}`,
        outlineOffset: '-2px',
        ':hover': {
            backgroundColor: tokens.colorBrandBackground2,
        },
    },
    dropBelow: {
        '::after': {
            content: '""',
            position: 'absolute',
            bottom: '-1px',
            left: 0,
            right: 0,
            height: '2px',
            backgroundColor: tokens.colorBrandForeground1,
            zIndex: 2,
        },
    },

    /* ── Drag handle ───────────────────────────────────────── */
    dragHandle: {
        cursor: 'grab',
        color: tokens.colorNeutralForeground4,
        display: 'flex',
        alignItems: 'center',
        flexShrink: 0,
        fontSize: '12px',
        opacity: 0.3,
        transitionProperty: 'opacity',
        transitionDuration: '0.15s',
        ':hover': {
            color: tokens.colorNeutralForeground1,
            opacity: 1,
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
    expandButton: {
        minWidth: '20px',
        width: '20px',
        height: '20px',
        padding: 0,
        flexShrink: 0,
    },
    leafSpacer: {
        width: '20px',
        flexShrink: 0,
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
    childCount: {
        flexShrink: 0,
        marginLeft: '4px',
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

interface BacklogTreeTableProps {
    items: WorkItem[]
    levelMap?: Map<number, WorkItemLevel>
    selectedItemId?: number | null
    onItemClick?: (item: WorkItem) => void
    onReparent?: (itemId: number, newParentId: number | null) => void
    onTitleChange?: (itemId: number, newTitle: string) => void
}

export function BacklogTreeTable({ items, levelMap, selectedItemId, onItemClick, onReparent, onTitleChange }: BacklogTreeTableProps) {
    const styles = useStyles()

    /* ── Build tree structure ──────────────────────────────── */
    const { roots, childrenMap } = useMemo(() => {
        const idSet = new Set(items.map((i) => i.workItemNumber))
        const cMap = new Map<number, WorkItem[]>()
        const rootItems: WorkItem[] = []

        for (const item of items) {
            if (item.parentWorkItemNumber == null || !idSet.has(item.parentWorkItemNumber)) {
                rootItems.push(item)
            } else {
                const siblings = cMap.get(item.parentWorkItemNumber) ?? []
                siblings.push(item)
                cMap.set(item.parentWorkItemNumber, siblings)
            }
        }

        const sortItems = (a: WorkItem, b: WorkItem) => a.priority - b.priority || a.workItemNumber - b.workItemNumber
        rootItems.sort(sortItems)
        for (const children of cMap.values()) {
            children.sort(sortItems)
        }

        return { roots: rootItems, childrenMap: cMap }
    }, [items])

    /* ── Track expanded state ──────────────────────────────── */
    const [expanded, setExpanded] = useState<Set<number>>(() => {
        const initial = new Set<number>()
        for (const root of roots) {
            if (childrenMap.has(root.workItemNumber)) initial.add(root.workItemNumber)
        }
        return initial
    })

    // Auto-expand parents when new children arrive (e.g., LLM creates nested items)
    useEffect(() => {
        setExpanded((prev) => {
            const next = new Set(prev)
            let changed = false
            for (const parentId of childrenMap.keys()) {
                if (!next.has(parentId)) {
                    next.add(parentId)
                    changed = true
                }
            }
            return changed ? next : prev
        })
    }, [childrenMap])

    const toggleExpanded = useCallback((id: number) => {
        setExpanded((prev) => {
            const next = new Set(prev)
            if (next.has(id)) next.delete(id)
            else next.add(id)
            return next
        })
    }, [])

    /* ── Inline title editing ──────────────────────────────── */
    const [editingId, setEditingId] = useState<number | null>(null)
    const [editTitle, setEditTitle] = useState('')
    const editInputRef = useRef<HTMLInputElement>(null)

    const startEditing = useCallback((item: WorkItem, e: React.MouseEvent) => {
        e.stopPropagation()
        setEditingId(item.workItemNumber)
        setEditTitle(item.title)
        // Focus the input after React renders it
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

    /* ── Drag & Drop state ─────────────────────────────────── */
    const [draggedId, setDraggedId] = useState<number | null>(null)
    const [dropTarget, setDropTarget] = useState<{ id: number; zone: DropZone } | null>(null)
    const [draggableRowId, setDraggableRowId] = useState<number | null>(null)

    /** Check if targetId is a descendant of ancestorId */
    const isDescendant = useCallback((targetId: number, ancestorId: number): boolean => {
        const visited = new Set<number>()
        const check = (id: number): boolean => {
            if (visited.has(id)) return false
            visited.add(id)
            const children = childrenMap.get(id) ?? []
            for (const child of children) {
                if (child.workItemNumber === targetId || check(child.workItemNumber)) return true
            }
            return false
        }
        return check(ancestorId)
    }, [childrenMap])

    /** Find the parent id of an item */
    const findParentId = useCallback((itemId: number): number | null => {
        const item = items.find((i) => i.workItemNumber === itemId)
        return item?.parentWorkItemNumber ?? null
    }, [items])

    const handleDragStart = useCallback((e: React.DragEvent<HTMLDivElement>, itemId: number) => {
        setDraggedId(itemId)
        e.dataTransfer.effectAllowed = 'move'
        e.dataTransfer.setData('text/plain', String(itemId))
    }, [])

    const handleDragOver = useCallback((e: React.DragEvent<HTMLDivElement>, rowItemId: number) => {
        e.preventDefault()
        if (draggedId == null || draggedId === rowItemId) {
            setDropTarget(null)
            return
        }
        // Don't allow dropping on own descendant
        if (isDescendant(rowItemId, draggedId)) {
            setDropTarget(null)
            e.dataTransfer.dropEffect = 'none'
            return
        }
        e.dataTransfer.dropEffect = 'move'

        // Determine zone based on cursor Y position within the row
        const rect = e.currentTarget.getBoundingClientRect()
        const y = e.clientY - rect.top
        const height = rect.height
        const quarter = height / 4

        let zone: DropZone
        if (y < quarter) {
            zone = 'above'
        } else if (y > height - quarter) {
            zone = 'below'
        } else {
            zone = 'on' // Make child
        }

        setDropTarget({ id: rowItemId, zone })
    }, [draggedId, isDescendant])

    const handleDragLeave = useCallback((e: React.DragEvent<HTMLDivElement>) => {
        // Only clear if leaving the row entirely (not entering a child element)
        if (!e.currentTarget.contains(e.relatedTarget as Node)) {
            setDropTarget(null)
        }
    }, [])

    const handleDrop = useCallback((e: React.DragEvent<HTMLDivElement>) => {
        e.preventDefault()
        if (draggedId == null || dropTarget == null || !onReparent) {
            setDraggedId(null)
            setDropTarget(null)
            return
        }

        const { id: targetId, zone } = dropTarget

        if (zone === 'on') {
            // Make child of target
            onReparent(draggedId, targetId)
            // Auto-expand the target so user sees the dropped item
            setExpanded((prev) => new Set(prev).add(targetId))
        } else {
            // 'above' or 'below' — make sibling (same parent as the target)
            const targetParent = findParentId(targetId)
            onReparent(draggedId, targetParent)
        }

        setDraggedId(null)
        setDropTarget(null)
    }, [draggedId, dropTarget, onReparent, findParentId])

    const handleDragEnd = useCallback(() => {
        setDraggedId(null)
        setDropTarget(null)
    }, [])

    /* ── Flatten tree into visible rows ────────────────────── */
    const visibleRows = useMemo(() => {
        const rows: Array<{ item: WorkItem; depth: number; hasChildren: boolean }> = []
        function walk(item: WorkItem, depth: number) {
            const children = childrenMap.get(item.workItemNumber)
            rows.push({ item, depth, hasChildren: (children?.length ?? 0) > 0 })
            if (children && expanded.has(item.workItemNumber)) {
                for (const child of children) walk(child, depth + 1)
            }
        }
        for (const root of roots) walk(root, 0)
        return rows
    }, [roots, childrenMap, expanded])

    /* ── Resolve level info ────────────────────────────────── */
    const getLevel = (item: WorkItem): WorkItemLevel | undefined =>
        item.levelId != null ? levelMap?.get(item.levelId) : undefined

    /* ── Get drop zone class for a row ─────────────────────── */
    const getDropClass = (itemId: number): string | false => {
        if (dropTarget?.id !== itemId) return false
        if (dropTarget.zone === 'above') return styles.dropAbove
        if (dropTarget.zone === 'on') return styles.dropOn
        if (dropTarget.zone === 'below') return styles.dropBelow
        return false
    }

    return (
        <div className={styles.container}>
            {/* header */}
            <div className={styles.header}>
                <Caption1 className={styles.headerText}>Type</Caption1>
                <Caption1 className={styles.headerText}>Title</Caption1>
                <Caption1 className={styles.headerText}>State</Caption1>
                <Caption1 className={styles.headerText}>ID</Caption1>
                <Caption1 className={styles.headerText}>Difficulty</Caption1>
                <Caption1 className={styles.headerText}>Assigned To</Caption1>
                <Caption1 className={styles.headerText}>Tags</Caption1>
            </div>

            {/* rows */}
            {visibleRows.map(({ item, depth, hasChildren }) => {
                const level = getLevel(item)
                const isSelected = item.workItemNumber === selectedItemId
                const isDragging = item.workItemNumber === draggedId
                const dropClass = getDropClass(item.workItemNumber)
                const isEditing = editingId === item.workItemNumber

                return (
                    <div
                        key={item.workItemNumber}
                        className={mergeClasses(
                            styles.row,
                            isSelected && styles.rowSelected,
                            isDragging && styles.rowDragging,
                            dropClass || undefined,
                        )}
                        style={{ borderLeftColor: level?.color ?? 'transparent' }}
                        onClick={() => {
                            if (!isEditing) onItemClick?.(item)
                        }}
                        draggable={draggableRowId === item.workItemNumber}
                        onDragStart={(e) => handleDragStart(e, item.workItemNumber)}
                        onDragOver={(e) => handleDragOver(e, item.workItemNumber)}
                        onDragLeave={handleDragLeave}
                        onDrop={handleDrop}
                        onDragEnd={() => { handleDragEnd(); setDraggableRowId(null) }}
                    >
                        {/* Work Item Type */}
                        <div className={styles.typeCell}>
                            <span
                                className={styles.dragHandle}
                                onMouseDown={(e) => { e.stopPropagation(); setDraggableRowId(item.workItemNumber) }}
                                onMouseUp={() => setDraggableRowId(null)}
                            >
                                <ReOrderRegular />
                            </span>
                            <Text className={styles.typeName}>
                                {level?.name ?? '-'}
                            </Text>
                        </div>

                        {/* Title */}
                        <div className={styles.titleCell} style={{ paddingLeft: `${depth * 24}px` }}>
                            {hasChildren ? (
                                <Button
                                    className={styles.expandButton}
                                    appearance="subtle"
                                    size="small"
                                    icon={expanded.has(item.workItemNumber) ? <ChevronDownRegular /> : <ChevronRightRegular />}
                                    onClick={(e) => { e.stopPropagation(); toggleExpanded(item.workItemNumber) }}
                                    aria-label={expanded.has(item.workItemNumber) ? 'Collapse' : 'Expand'}
                                />
                            ) : (
                                <span className={styles.leafSpacer} />
                            )}

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

                            {hasChildren && !isEditing && (
                                <Badge className={styles.childCount} appearance="tint" size="tiny" color="informative">
                                    {childrenMap.get(item.workItemNumber)?.length ?? 0}
                                </Badge>
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

