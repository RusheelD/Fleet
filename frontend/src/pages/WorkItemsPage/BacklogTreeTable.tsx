import { useState, useMemo, useCallback, useRef, useEffect } from 'react'
import {
    makeStyles,
    Caption1,
    Text,
    Button,
    Input,
    Checkbox,
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
import {
    buildWorkItemGridTemplateColumns,
    type WorkItemTableColumnKey,
} from './workItemTableColumns'
import { appTokens } from '../../styles/appTokens'
import { InfoBadge } from '../../components/shared/InfoBadge'
import { useResizableWorkItemColumns } from './useResizableWorkItemColumns'

/* ── Drop zone enum ────────────────────────────────────────── */
type DropZone = 'above' | 'on' | 'below' | null

const useStyles = makeStyles({
    container: {
        flex: 1,
        overflow: 'auto',
        display: 'flex',
        flexDirection: 'column',
        borderTop: appTokens.border.subtle,
    },

    /* ── header row ────────────────────────────────────────── */
    header: {
        display: 'grid',
        alignItems: 'center',
        paddingTop: appTokens.space.xs,
        paddingBottom: appTokens.space.xs,
        paddingLeft: appTokens.space.md,
        paddingRight: appTokens.space.md,
        gap: appTokens.space.sm,
        borderBottom: appTokens.border.subtle,
        backgroundColor: appTokens.color.surfaceAlt,
        position: 'sticky',
        top: 0,
        zIndex: 1,
        borderLeft: '3px solid transparent',
    },
    headerText: {
        fontSize: appTokens.fontSize.xs,
        fontWeight: appTokens.fontWeight.semibold,
        color: appTokens.color.textTertiary,
        textTransform: 'uppercase',
        letterSpacing: '0.05em',
    },
    selectHeaderCell: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
    },

    /* ── data rows ─────────────────────────────────────────── */
    row: {
        display: 'grid',
        alignItems: 'center',
        paddingTop: '3px',
        paddingBottom: '3px',
        paddingLeft: appTokens.space.md,
        paddingRight: appTokens.space.md,
        gap: appTokens.space.sm,
        borderBottom: appTokens.border.subtleAlt,
        borderLeft: '3px solid transparent',
        cursor: 'pointer',
        minHeight: '34px',
        position: 'relative',
        ':hover': {
            backgroundColor: appTokens.color.surfaceHover,
        },
    },
    rowSelected: {
        backgroundColor: appTokens.color.surfaceBrand,
        ':hover': {
            backgroundColor: appTokens.color.surfaceBrand,
        },
    },
    rowDragging: {
        opacity: 0.4,
    },
    selectCell: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
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
            backgroundColor: appTokens.color.brand,
            zIndex: 2,
        },
    },
    dropOn: {
        backgroundColor: appTokens.color.surfaceBrand,
        outline: `2px solid ${appTokens.color.brand}`,
        outlineOffset: '-2px',
        ':hover': {
            backgroundColor: appTokens.color.surfaceBrand,
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
            backgroundColor: appTokens.color.brand,
            zIndex: 2,
        },
    },

    /* ── Drag handle ───────────────────────────────────────── */
    dragHandle: {
        cursor: 'grab',
        color: appTokens.color.textMuted,
        display: 'flex',
        alignItems: 'center',
        flexShrink: 0,
        fontSize: appTokens.fontSize.sm,
        opacity: 0.3,
        transitionProperty: 'opacity',
        transitionDuration: appTokens.motion.fast,
        ':hover': {
            color: appTokens.color.textPrimary,
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
        fontSize: appTokens.fontSize.sm,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        color: appTokens.color.textSecondary,
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
        flexGrow: 0,
        flexShrink: 1,
        minWidth: 0,
        maxWidth: '100%',
    },
    titleInput: {
        flexGrow: 1,
        minWidth: 0,
    },
    childCountChip: {
        flexShrink: 0,
        marginLeft: '6px',
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
        height: '20px',
        minWidth: '22px',
        paddingLeft: '6px',
        paddingRight: '6px',
        borderRadius: '999px',
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
        backgroundColor: appTokens.color.surfaceAlt,
    },
    childCountNumber: {
        fontSize: appTokens.fontSize.sm,
        fontWeight: appTokens.fontWeight.semibold,
        color: appTokens.color.textPrimary,
        lineHeight: '1',
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
        color: appTokens.color.textSecondary,
        fontSize: appTokens.fontSize.sm,
    },

    /* ── Difficulty column ──────────────────────────────────── */
    difficultyText: {
        fontSize: appTokens.fontSize.sm,
        color: appTokens.color.textSecondary,
    },

    /* ── Assigned To column ────────────────────────────────── */
    assigneeCell: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
        color: appTokens.color.textSecondary,
        fontSize: appTokens.fontSize.sm,
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
        alignItems: 'center',
        gap: '6px',
        overflow: 'hidden',
        minWidth: 0,
    },
    tagBadge: {
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontSize: appTokens.fontSize.sm,
        lineHeight: '18px',
        fontWeight: appTokens.fontWeight.medium,
        paddingLeft: '8px',
        paddingRight: '8px',
        flex: '1 1 0',
        maxWidth: '100%',
        minWidth: 0,
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        wordBreak: 'normal',
        overflowWrap: 'normal',
    },
    resizeHandle: {
        position: 'absolute',
        top: '-6px',
        right: '-7px',
        width: '14px',
        height: 'calc(100% + 12px)',
        cursor: 'col-resize',
        zIndex: 2,
        display: 'flex',
        alignItems: 'stretch',
        justifyContent: 'center',
        touchAction: 'none',
        '::after': {
            content: '""',
            width: '2px',
            borderRadius: '999px',
            backgroundColor: appTokens.color.border,
            transitionProperty: 'background-color',
            transitionDuration: appTokens.motion.fast,
        },
        ':hover::after': {
            backgroundColor: appTokens.color.brand,
        },
    },
    headerCell: {
        position: 'relative',
        minWidth: 0,
        display: 'flex',
        alignItems: 'center',
        paddingRight: '6px',
    },
})

interface BacklogTreeTableProps {
    items: WorkItem[]
    levelMap?: Map<number, WorkItemLevel>
    selectedItemId?: number | null
    selectedWorkItemNumbers?: Set<number>
    onItemClick?: (item: WorkItem) => void
    onToggleSelection?: (itemNumber: number, selected: boolean) => void
    onToggleSelectionForItems?: (itemNumbers: number[], selected: boolean) => void
    onReparent?: (itemId: number, newParentId: number | null) => void
    onTitleChange?: (itemId: number, newTitle: string) => void
    columnWidths: Record<WorkItemTableColumnKey, number>
    collapsedColumns: ReadonlySet<WorkItemTableColumnKey>
    onResizeColumn?: (column: WorkItemTableColumnKey, width: number) => void
}

export function BacklogTreeTable({
    items,
    levelMap,
    selectedItemId,
    selectedWorkItemNumbers,
    onItemClick,
    onToggleSelection,
    onToggleSelectionForItems,
    onReparent,
    onTitleChange,
    columnWidths,
    collapsedColumns,
    onResizeColumn,
}: BacklogTreeTableProps) {
    const styles = useStyles()
    const gridTemplateColumns = useMemo(
        () => buildWorkItemGridTemplateColumns(columnWidths, collapsedColumns),
        [columnWidths, collapsedColumns],
    )
    const startResizingColumn = useResizableWorkItemColumns({
        columnWidths,
        onResizeColumn,
    })
    const isColumnVisible = useCallback(
        (column: WorkItemTableColumnKey) => !collapsedColumns.has(column),
        [collapsedColumns],
    )

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
    const itemById = useMemo(() => {
        const map = new Map<number, WorkItem>()
        for (const item of items) {
            map.set(item.workItemNumber, item)
        }
        return map
    }, [items])

    const isResolvedLikeState = useCallback((state: string) =>
        state === 'Resolved' || state === 'Resolved (AI)' || state === 'Closed',
    [])

    const areAllDescendantsResolved = useCallback((itemId: number): boolean => {
        const queue = [...(childrenMap.get(itemId) ?? [])]
        if (queue.length === 0) {
            return false
        }

        while (queue.length > 0) {
            const current = queue.shift()!
            if (!isResolvedLikeState(current.state)) {
                return false
            }

            const children = childrenMap.get(current.workItemNumber)
            if (children?.length) {
                queue.push(...children)
            }
        }

        return true
    }, [childrenMap, isResolvedLikeState])

    const shouldAutoCollapseNode = useCallback((itemId: number): boolean => {
        const item = itemById.get(itemId)
        if (!item) {
            return false
        }

        return isResolvedLikeState(item.state) && areAllDescendantsResolved(itemId)
    }, [areAllDescendantsResolved, isResolvedLikeState, itemById])

    /* ── Track expanded state ──────────────────────────────── */
    const [expanded, setExpanded] = useState<Set<number>>(() => {
        const initial = new Set<number>()
        for (const root of roots) {
            if (childrenMap.has(root.workItemNumber) && !shouldAutoCollapseNode(root.workItemNumber)) {
                initial.add(root.workItemNumber)
            }
        }
        return initial
    })

    // Auto-expand active parents when new children arrive, but auto-collapse
    // resolved branches whose descendants are all resolved/closed.
    useEffect(() => {
        setExpanded((prev) => {
            const next = new Set(prev)
            let changed = false
            for (const parentId of childrenMap.keys()) {
                if (shouldAutoCollapseNode(parentId)) {
                    if (next.delete(parentId)) {
                        changed = true
                    }
                    continue
                }

                if (!next.has(parentId)) {
                    next.add(parentId)
                    changed = true
                }
            }
            return changed ? next : prev
        })
    }, [childrenMap, shouldAutoCollapseNode])

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

    const visibleWorkItemNumbers = useMemo(
        () => visibleRows.map(({ item }) => item.workItemNumber),
        [visibleRows],
    )

    const selectedVisibleCount = useMemo(
        () => visibleWorkItemNumbers.filter((itemNumber) => selectedWorkItemNumbers?.has(itemNumber)).length,
        [visibleWorkItemNumbers, selectedWorkItemNumbers],
    )

    const areAllVisibleSelected =
        visibleWorkItemNumbers.length > 0 &&
        selectedVisibleCount === visibleWorkItemNumbers.length

    const isVisibleSelectionMixed =
        selectedVisibleCount > 0 &&
        !areAllVisibleSelected

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
            <div className={styles.header} style={{ gridTemplateColumns }}>
                <div className={styles.selectHeaderCell}>
                    <Checkbox
                        aria-label="Select visible work items"
                        checked={areAllVisibleSelected ? true : (isVisibleSelectionMixed ? 'mixed' : false)}
                        onChange={(_event, data) => {
                            onToggleSelectionForItems?.(
                                visibleWorkItemNumbers,
                                data.checked === true,
                            )
                        }}
                    />
                </div>
                {isColumnVisible('type') && (
                    <div className={styles.headerCell}>
                        <Caption1 className={styles.headerText}>Type</Caption1>
                        <span
                            className={styles.resizeHandle}
                            onMouseDown={(event) => startResizingColumn(event, 'type')}
                            aria-hidden="true"
                        />
                    </div>
                )}
                {isColumnVisible('title') && (
                    <div className={styles.headerCell}>
                        <Caption1 className={styles.headerText}>Title</Caption1>
                        <span
                            className={styles.resizeHandle}
                            onMouseDown={(event) => startResizingColumn(event, 'title')}
                            aria-hidden="true"
                        />
                    </div>
                )}
                {isColumnVisible('state') && (
                    <div className={styles.headerCell}>
                        <Caption1 className={styles.headerText}>State</Caption1>
                        <span
                            className={styles.resizeHandle}
                            onMouseDown={(event) => startResizingColumn(event, 'state')}
                            aria-hidden="true"
                        />
                    </div>
                )}
                {isColumnVisible('id') && (
                    <div className={styles.headerCell}>
                        <Caption1 className={styles.headerText}>ID</Caption1>
                        <span
                            className={styles.resizeHandle}
                            onMouseDown={(event) => startResizingColumn(event, 'id')}
                            aria-hidden="true"
                        />
                    </div>
                )}
                {isColumnVisible('difficulty') && (
                    <div className={styles.headerCell}>
                        <Caption1 className={styles.headerText}>Difficulty</Caption1>
                        <span
                            className={styles.resizeHandle}
                            onMouseDown={(event) => startResizingColumn(event, 'difficulty')}
                            aria-hidden="true"
                        />
                    </div>
                )}
                {isColumnVisible('assignedTo') && (
                    <div className={styles.headerCell}>
                        <Caption1 className={styles.headerText}>Assigned To</Caption1>
                        <span
                            className={styles.resizeHandle}
                            onMouseDown={(event) => startResizingColumn(event, 'assignedTo')}
                            aria-hidden="true"
                        />
                    </div>
                )}
                {isColumnVisible('tags') && (
                    <div className={styles.headerCell}>
                        <Caption1 className={styles.headerText}>Tags</Caption1>
                        <span
                            className={styles.resizeHandle}
                            onMouseDown={(event) => startResizingColumn(event, 'tags')}
                            aria-hidden="true"
                        />
                    </div>
                )}
            </div>

            {/* rows */}
            {visibleRows.map(({ item, depth, hasChildren }) => {
                const level = getLevel(item)
                const isSelected = item.workItemNumber === selectedItemId
                const isDragging = item.workItemNumber === draggedId
                const dropClass = getDropClass(item.workItemNumber)
                const isEditing = editingId === item.workItemNumber
                const childCount = childrenMap.get(item.workItemNumber)?.length ?? 0

                return (
                    <div
                        key={item.workItemNumber}
                        className={mergeClasses(
                            styles.row,
                            isSelected && styles.rowSelected,
                            isDragging && styles.rowDragging,
                            dropClass || undefined,
                        )}
                        style={{
                            borderLeftColor: level?.color ?? 'transparent',
                            gridTemplateColumns,
                        }}
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
                        <div className={styles.selectCell} onClick={(event) => event.stopPropagation()}>
                            <Checkbox
                                aria-label={`Select work item #${item.workItemNumber}`}
                                checked={selectedWorkItemNumbers?.has(item.workItemNumber) ?? false}
                                onChange={(_event, data) => onToggleSelection?.(item.workItemNumber, data.checked === true)}
                            />
                        </div>
                        {isColumnVisible('type') && (
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
                        )}

                        {isColumnVisible('title') && (
                            <div className={styles.titleCell} style={{ paddingLeft: `${depth * 24}px` }}>
                                {!isColumnVisible('type') && (
                                    <span
                                        className={styles.dragHandle}
                                        onMouseDown={(e) => { e.stopPropagation(); setDraggableRowId(item.workItemNumber) }}
                                        onMouseUp={() => setDraggableRowId(null)}
                                    >
                                        <ReOrderRegular />
                                    </span>
                                )}
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

                            {hasChildren && !isEditing && childCount > 0 && (
                                <span
                                    className={styles.childCountChip}
                                    title={`${childCount} ${childCount === 1 ? 'child' : 'children'}`}
                                >
                                    <span className={styles.childCountNumber}>{childCount}</span>
                                </span>
                            )}
                            </div>
                        )}

                        {isColumnVisible('state') && (
                            <div className={styles.stateCell}>
                                <StateDot state={item.state} />
                                <Text className={styles.stateText}>{formatWorkItemState(item.state)}</Text>
                            </div>
                        )}

                        {isColumnVisible('id') && (
                            <Text className={styles.idText}>{item.workItemNumber}</Text>
                        )}

                        {isColumnVisible('difficulty') && (
                            <Text className={styles.difficultyText}>
                                {(['', 'D1', 'D2', 'D3', 'D4', 'D5'] as const)[item.difficulty] ?? '-'}
                            </Text>
                        )}

                        {isColumnVisible('assignedTo') && (
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
                        )}

                        {isColumnVisible('tags') && (
                            <div className={styles.tagsCell}>
                                {item.tags.slice(0, 3).map((tag) => (
                                    <InfoBadge
                                        key={tag}
                                        appearance="tint"
                                        size="small"
                                        className={styles.tagBadge}
                                    >
                                        {tag}
                                    </InfoBadge>
                                ))}
                            </div>
                        )}
                    </div>
                )
            })}
        </div>
    )
}
