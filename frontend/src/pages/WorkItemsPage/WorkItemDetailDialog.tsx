import { useState, useEffect, useMemo, useCallback } from 'react'
import {
    makeStyles,
    tokens,
    Button,
    Input,
    Dropdown,
    Option,
    Textarea,
    Badge,
    Text,
    Caption1,
    Divider,
    Tooltip,
} from '@fluentui/react-components'
import {
    SaveRegular,
    DeleteRegular,
    DismissRegular,
    BotRegular,
    PersonRegular,
    ChevronRightRegular,
} from '@fluentui/react-icons'
import { useUpdateWorkItem, useDeleteWorkItem, resolveLevelIcon } from '../../proxies'
import type { WorkItem, WorkItemLevel } from '../../models'
import { StateDot } from './StateDot'
import { PriorityDot } from './PriorityDot'

const NONE_PARENT = '(None)'
const NONE_LEVEL = '(None)'

const PRIORITY_LABELS: Record<number, string> = {
    1: 'P1 — Critical',
    2: 'P2 — High',
    3: 'P3 — Medium',
    4: 'P4 — Low',
}

const PRIORITY_MAP: Record<string, number> = {
    'P1 — Critical': 1,
    'P2 — High': 2,
    'P3 — Medium': 3,
    'P4 — Low': 4,
}

const useStyles = makeStyles({
    /* ── Overlay backdrop ──────────────────────────────────── */
    overlay: {
        position: 'fixed',
        inset: 0,
        backgroundColor: 'rgba(0, 0, 0, 0.3)',
        zIndex: 900,
    },

    /* ── Panel container ───────────────────────────────────── */
    panel: {
        position: 'fixed',
        top: 0,
        right: 0,
        bottom: 0,
        width: '960px',
        maxWidth: '100vw',
        backgroundColor: tokens.colorNeutralBackground1,
        boxShadow: tokens.shadow64,
        zIndex: 1000,
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
    },

    /* ── Header bar (type badge + ID + title) ──────────────── */
    headerBar: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalM,
        paddingTop: tokens.spacingVerticalM,
        paddingBottom: tokens.spacingVerticalM,
        paddingLeft: tokens.spacingHorizontalL,
        paddingRight: tokens.spacingHorizontalM,
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: tokens.colorNeutralBackground2,
        flexShrink: 0,
    },
    headerType: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
        flexShrink: 0,
    },
    headerTypeIcon: {
        fontSize: '20px',
        display: 'flex',
        alignItems: 'center',
    },
    headerId: {
        color: tokens.colorNeutralForeground2,
        fontSize: '14px',
        flexShrink: 0,
    },
    headerTitle: {
        flex: 1,
        minWidth: 0,
    },
    headerTitleInput: {
        fontWeight: tokens.fontWeightSemibold,
        fontSize: tokens.fontSizeBase400,
    },
    headerActions: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        flexShrink: 0,
    },

    /* ── Action bar (Save / Delete) ────────────────────────── */
    actionBar: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        paddingTop: tokens.spacingVerticalS,
        paddingBottom: tokens.spacingVerticalS,
        paddingLeft: tokens.spacingHorizontalL,
        paddingRight: tokens.spacingHorizontalL,
        borderBottom: `1px solid ${tokens.colorNeutralStroke3}`,
        flexShrink: 0,
    },
    actionBarLeft: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    deleteButton: {
        color: tokens.colorPaletteRedForeground1,
    },

    /* ── Scrollable body ──────────────────────────────────── */
    body: {
        display: 'flex',
        flexDirection: 'column',
        flex: 1,
        overflow: 'auto',
        paddingTop: tokens.spacingVerticalL,
        paddingBottom: tokens.spacingVerticalXL,
        paddingLeft: tokens.spacingHorizontalXL,
        paddingRight: tokens.spacingHorizontalXL,
        gap: tokens.spacingVerticalL,
    },

    /* ── Fields grid (compact strip across top) ────────────── */
    fieldsGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(3, 1fr)',
        gap: `${tokens.spacingVerticalM} ${tokens.spacingHorizontalL}`,
        paddingTop: tokens.spacingVerticalS,
        paddingBottom: tokens.spacingVerticalM,
        borderBottom: `1px solid ${tokens.colorNeutralStroke3}`,
    },

    /* ── Description (full width, fills remaining space) ───── */
    descriptionSection: {
        display: 'flex',
        flexDirection: 'column',
        flex: 1,
        minHeight: '200px',
    },
    descriptionTextarea: {
        marginTop: '8px',
        flex: 1,
        '& > textarea': {
            minHeight: '200px',
        },
    },

    /* ── Section headers ───────────────────────────────────── */
    sectionTitle: {
        fontWeight: tokens.fontWeightSemibold,
        fontSize: '13px',
        color: tokens.colorNeutralForeground2,
        textTransform: 'uppercase',
        letterSpacing: '0.03em',
    },

    /* ── Field rows ────────────────────────────────────────── */
    fieldRow: {
        display: 'flex',
        flexDirection: 'column',
        gap: '2px',
    },
    fieldLabel: {
        fontSize: '11px',
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground3,
        textTransform: 'uppercase',
        letterSpacing: '0.04em',
    },
    fieldValue: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
        fontSize: '13px',
        minHeight: '28px',
    },
    fieldDropdown: {
        minWidth: 0,
        width: '100%',
    },

    /* ── Children section ──────────────────────────────────── */
    childRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
        padding: '4px 8px',
        borderRadius: tokens.borderRadiusMedium,
        cursor: 'default',
        fontSize: '13px',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },
    childIcon: {
        fontSize: '14px',
        display: 'flex',
        alignItems: 'center',
        flexShrink: 0,
    },
    childTitle: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        flex: 1,
    },
    childId: {
        color: tokens.colorNeutralForeground3,
        fontSize: '12px',
        flexShrink: 0,
    },

    /* ── AI badge ──────────────────────────────────────────── */
    aiBadge: {
        flexShrink: 0,
    },
})

interface WorkItemDetailDialogProps {
    projectId: string
    item: WorkItem | null
    workItems?: WorkItem[]
    levels?: WorkItemLevel[]
    onClose: () => void
    onNavigate?: (item: WorkItem) => void
}

export function WorkItemDetailDialog({ projectId, item, workItems, levels, onClose, onNavigate }: WorkItemDetailDialogProps) {
    const styles = useStyles()
    const updateMutation = useUpdateWorkItem(projectId)
    const deleteMutation = useDeleteWorkItem(projectId)

    const [title, setTitle] = useState('')
    const [description, setDescription] = useState('')
    const [priorityLabel, setPriorityLabel] = useState('P2 — High')
    const [state, setState] = useState('New')
    const [tags, setTags] = useState('')
    const [assignedTo, setAssignedTo] = useState('')
    const [parentLabel, setParentLabel] = useState(NONE_PARENT)
    const [levelLabel, setLevelLabel] = useState(NONE_LEVEL)

    const sortedLevels = useMemo(() => [...(levels ?? [])].sort((a, b) => a.ordinal - b.ordinal), [levels])

    const parentOptions = useMemo(() => {
        if (!item || !workItems) return []
        const descendantIds = new Set<number>()
        const collectDescendants = (witNum: number) => {
            for (const wi of workItems) {
                if (wi.parentWorkItemNumber === witNum && !descendantIds.has(wi.workItemNumber)) {
                    descendantIds.add(wi.workItemNumber)
                    collectDescendants(wi.workItemNumber)
                }
            }
        }
        descendantIds.add(item.workItemNumber)
        collectDescendants(item.workItemNumber)
        return workItems.filter((wi) => !descendantIds.has(wi.workItemNumber))
    }, [item, workItems])

    const children = useMemo(() => {
        if (!item || !workItems) return []
        return workItems.filter((wi) => wi.parentWorkItemNumber === item.workItemNumber)
    }, [item, workItems])

    const currentLevel = useMemo(() => {
        const selected = sortedLevels.find((l) => l.name === levelLabel)
        if (selected) return selected
        if (item?.levelId != null) return levels?.find((l) => l.id === item.levelId)
        return undefined
    }, [levelLabel, sortedLevels, item, levels])

    useEffect(() => {
        if (item) {
            setTitle(item.title)
            setDescription(item.description)
            setPriorityLabel(PRIORITY_LABELS[item.priority] ?? 'P2 — High')
            setState(item.state)
            setTags(item.tags.join(', '))
            setAssignedTo(item.assignedTo)
            const parent = workItems?.find((wi) => wi.workItemNumber === item.parentWorkItemNumber)
            setParentLabel(parent ? `#${parent.workItemNumber} ${parent.title}` : NONE_PARENT)
            const lvl = sortedLevels.find((l) => l.id === item.levelId)
            setLevelLabel(lvl ? lvl.name : NONE_LEVEL)
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps -- reset form only when item changes, not on every levels refetch
    }, [item?.workItemNumber])

    /* ── Escape key handler ─────────────────────────────────── */
    const handleKeyDown = useCallback((e: KeyboardEvent) => {
        if (e.key === 'Escape') onClose()
    }, [onClose])

    useEffect(() => {
        if (item) {
            document.addEventListener('keydown', handleKeyDown)
            return () => document.removeEventListener('keydown', handleKeyDown)
        }
    }, [item, handleKeyDown])

    const handleSave = () => {
        if (!item || !title.trim()) return
        const selectedParent = parentOptions.find((wi) => `#${wi.workItemNumber} ${wi.title}` === parentLabel)
        const selectedLevel = sortedLevels.find((l) => l.name === levelLabel)
        updateMutation.mutate(
            {
                workItemNumber: item.workItemNumber,
                data: {
                    title: title.trim(),
                    description: description.trim(),
                    priority: PRIORITY_MAP[priorityLabel] ?? 2,
                    state,
                    assignedTo: assignedTo.trim() || 'Unassigned',
                    tags: tags
                        .split(',')
                        .map((t) => t.trim())
                        .filter(Boolean),
                    parentWorkItemNumber: parentLabel === NONE_PARENT ? 0 : (selectedParent?.workItemNumber ?? item.parentWorkItemNumber),
                    levelId: levelLabel === NONE_LEVEL ? 0 : (selectedLevel?.id ?? item.levelId),
                },
            },
            { onSuccess: () => onClose() },
        )
    }

    const handleDelete = () => {
        if (!item) return
        deleteMutation.mutate(item.workItemNumber, { onSuccess: () => onClose() })
    }

    if (!item) return null

    return (
        <>
            {/* Backdrop */}
            <div className={styles.overlay} onClick={onClose} />

            {/* Panel */}
            <div className={styles.panel}>
                {/* ── Header bar ──────────────────────────── */}
                <div className={styles.headerBar}>
                    <div className={styles.headerType}>
                        {currentLevel ? (
                            <>
                                <span className={styles.headerTypeIcon} style={{ color: currentLevel.color }}>
                                    {resolveLevelIcon(currentLevel.iconName)}
                                </span>
                                <Text weight="semibold" style={{ color: currentLevel.color }}>
                                    {currentLevel.name}
                                </Text>
                            </>
                        ) : (
                            <Text weight="semibold" style={{ color: tokens.colorNeutralForeground3 }}>
                                Work Item
                            </Text>
                        )}
                    </div>
                    <Text className={styles.headerId}>{item.workItemNumber}</Text>
                    {item.isAI && (
                        <Badge appearance="filled" color="brand" size="small" className={styles.aiBadge}>
                            AI
                        </Badge>
                    )}
                    <div className={styles.headerTitle}>
                        <Input
                            className={styles.headerTitleInput}
                            appearance="underline"
                            value={title}
                            onChange={(_e, data) => setTitle(data.value)}
                        />
                    </div>
                    <div className={styles.headerActions}>
                        <Tooltip content="Close" relationship="label">
                            <Button
                                appearance="subtle"
                                icon={<DismissRegular />}
                                onClick={onClose}
                                aria-label="Close"
                            />
                        </Tooltip>
                    </div>
                </div>

                {/* ── Action bar ──────────────────────────── */}
                <div className={styles.actionBar}>
                    <div className={styles.actionBarLeft}>
                        <Button
                            appearance="primary"
                            icon={<SaveRegular />}
                            size="small"
                            onClick={handleSave}
                            disabled={!title.trim() || updateMutation.isPending}
                        >
                            {updateMutation.isPending ? 'Saving…' : 'Save'}
                        </Button>
                        <Button appearance="subtle" size="small" onClick={onClose}>
                            Cancel
                        </Button>
                    </div>
                    <Button
                        appearance="subtle"
                        icon={<DeleteRegular />}
                        size="small"
                        className={styles.deleteButton}
                        onClick={handleDelete}
                        disabled={deleteMutation.isPending}
                    >
                        Delete
                    </Button>
                </div>

                {/* ── Body: fields grid → description → children ── */}
                <div className={styles.body}>
                    {/* Detail fields in a compact grid */}
                    <div className={styles.fieldsGrid}>
                        {/* State */}
                        <div className={styles.fieldRow}>
                            <Text className={styles.fieldLabel}>State</Text>
                            <Dropdown
                                className={styles.fieldDropdown}
                                size="small"
                                value={state}
                                onOptionSelect={(_e, data) => setState(data.optionText ?? 'New')}
                            >
                                <Option>New</Option>
                                <Option>Active</Option>
                                <Option>In Progress</Option>
                                <Option>Resolved</Option>
                                <Option>Closed</Option>
                            </Dropdown>
                        </div>

                        {/* Level */}
                        <div className={styles.fieldRow}>
                            <Text className={styles.fieldLabel}>Work Item Type</Text>
                            <Dropdown
                                className={styles.fieldDropdown}
                                size="small"
                                value={levelLabel}
                                onOptionSelect={(_e, data) => setLevelLabel(data.optionText ?? NONE_LEVEL)}
                            >
                                <Option>{NONE_LEVEL}</Option>
                                {sortedLevels.map((l) => (
                                    <Option key={l.id} text={l.name}>
                                        <span style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}>
                                            <span style={{ color: l.color, display: 'flex', alignItems: 'center' }}>
                                                {resolveLevelIcon(l.iconName)}
                                            </span>
                                            {l.name}
                                        </span>
                                    </Option>
                                ))}
                            </Dropdown>
                        </div>

                        {/* Priority */}
                        <div className={styles.fieldRow}>
                            <Text className={styles.fieldLabel}>Priority</Text>
                            <div className={styles.fieldValue}>
                                <PriorityDot priority={PRIORITY_MAP[priorityLabel] ?? 2} />
                                <Dropdown
                                    className={styles.fieldDropdown}
                                    size="small"
                                    value={priorityLabel}
                                    onOptionSelect={(_e, data) => setPriorityLabel(data.optionText ?? 'P2 — High')}
                                >
                                    <Option>P1 — Critical</Option>
                                    <Option>P2 — High</Option>
                                    <Option>P3 — Medium</Option>
                                    <Option>P4 — Low</Option>
                                </Dropdown>
                            </div>
                        </div>

                        {/* Assigned To */}
                        <div className={styles.fieldRow}>
                            <Text className={styles.fieldLabel}>Assigned To</Text>
                            <div className={styles.fieldValue}>
                                {item.isAI ? <BotRegular /> : <PersonRegular />}
                                <Input
                                    size="small"
                                    appearance="underline"
                                    value={assignedTo}
                                    onChange={(_e, data) => setAssignedTo(data.value)}
                                    style={{ flex: 1 }}
                                />
                            </div>
                        </div>

                        {/* Parent */}
                        <div className={styles.fieldRow}>
                            <Text className={styles.fieldLabel}>Parent</Text>
                            <Dropdown
                                className={styles.fieldDropdown}
                                size="small"
                                placeholder="Select parent"
                                value={parentLabel}
                                onOptionSelect={(_e, data) => setParentLabel(data.optionText ?? NONE_PARENT)}
                            >
                                <Option>{NONE_PARENT}</Option>
                                {parentOptions.map((wi) => (
                                    <Option key={wi.workItemNumber}>{`#${wi.workItemNumber} ${wi.title}`}</Option>
                                ))}
                            </Dropdown>
                        </div>

                        {/* Tags */}
                        <div className={styles.fieldRow}>
                            <Text className={styles.fieldLabel}>Tags</Text>
                            <Input
                                size="small"
                                appearance="underline"
                                placeholder="tag1, tag2, …"
                                value={tags}
                                onChange={(_e, data) => setTags(data.value)}
                            />
                        </div>
                    </div>

                    {/* Full-width description */}
                    <div className={styles.descriptionSection}>
                        <Text className={styles.sectionTitle}>Description</Text>
                        <Textarea
                            className={styles.descriptionTextarea}
                            value={description}
                            onChange={(_e, data) => setDescription(data.value)}
                            resize="vertical"
                            placeholder="Add a description…"
                        />
                    </div>

                    {/* Children */}
                    {children.length > 0 && (
                        <div>
                            <Divider />
                            <Text className={styles.sectionTitle} style={{ marginTop: '12px', marginBottom: '8px', display: 'block' }}>
                                Child Items ({children.length})
                            </Text>
                            {children.map((child) => {
                                const childLevel = child.levelId != null ? levels?.find((l) => l.id === child.levelId) : undefined
                                return (
                                    <div
                                        key={child.workItemNumber}
                                        className={styles.childRow}
                                        onClick={() => onNavigate?.(child)}
                                        style={{ cursor: onNavigate ? 'pointer' : 'default' }}
                                    >
                                        {childLevel && (
                                            <span className={styles.childIcon} style={{ color: childLevel.color }}>
                                                {resolveLevelIcon(childLevel.iconName)}
                                            </span>
                                        )}
                                        <Text className={styles.childId}>{child.workItemNumber}</Text>
                                        <ChevronRightRegular fontSize={10} />
                                        <Text className={styles.childTitle}>{child.title}</Text>
                                        <StateDot state={child.state} />
                                        <Caption1>{child.state}</Caption1>
                                    </div>
                                )
                            })}
                        </div>
                    )}
                </div>
            </div>
        </>
    )
}
