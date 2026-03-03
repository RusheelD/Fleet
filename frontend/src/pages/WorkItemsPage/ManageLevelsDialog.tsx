import { useState, useMemo, useCallback, useEffect } from 'react'
import {
    makeStyles,
    tokens,
    Button,
    Input,
    Field,
    Dialog,
    DialogSurface,
    DialogTitle,
    DialogBody,
    DialogContent,
    DialogActions,
    Text,
    Caption1,
    Tooltip,
    Dropdown,
    Option,
} from '@fluentui/react-components'
import {
    AddRegular,
    DeleteRegular,
    SaveRegular,
    ArrowUpRegular,
    ArrowDownRegular,
    DismissRegular,
    EditRegular,
} from '@fluentui/react-icons'
import {
    useWorkItemLevels,
    useCreateWorkItemLevel,
    useUpdateWorkItemLevel,
    useDeleteWorkItemLevel,
    resolveLevelIcon,
    LEVEL_ICON_NAMES,
} from '../../proxies'
import type { WorkItemLevel } from '../../models'

const useStyles = makeStyles({
    levelList: {
        display: 'flex',
        flexDirection: 'column',
        gap: '4px',
    },
    levelRow: {
        display: 'grid',
        gridTemplateColumns: '32px 1fr auto',
        alignItems: 'center',
        gap: '0.5rem',
        padding: '0.375rem 0.5rem',
        borderRadius: tokens.borderRadiusMedium,
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },
    levelPreview: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
    },
    levelIcon: {
        display: 'flex',
        alignItems: 'center',
        fontSize: '16px',
    },
    levelMeta: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.25rem',
    },
    levelActions: {
        display: 'flex',
        alignItems: 'center',
        gap: '2px',
    },
    editForm: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0.75rem',
        padding: '0.75rem',
        borderRadius: tokens.borderRadiusMedium,
        backgroundColor: tokens.colorNeutralBackground3,
    },
    editFormRow: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: '0.75rem',
    },
    colorPreview: {
        width: '24px',
        height: '24px',
        borderRadius: '4px',
        border: `1px solid ${tokens.colorNeutralStroke1}`,
        cursor: 'pointer',
        flexShrink: 0,
    },
    iconGrid: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: '2px',
    },
    iconOption: {
        width: '28px',
        height: '28px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        borderRadius: tokens.borderRadiusMedium,
        cursor: 'pointer',
        fontSize: '14px',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },
    iconOptionSelected: {
        width: '28px',
        height: '28px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        borderRadius: tokens.borderRadiusMedium,
        cursor: 'pointer',
        fontSize: '14px',
        backgroundColor: tokens.colorBrandBackground2,
        outline: `2px solid ${tokens.colorBrandStroke1}`,
    },
    emptyState: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: '0.5rem',
        padding: '1.5rem',
        color: tokens.colorNeutralForeground3,
    },
    dialogContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0.75rem',
        maxHeight: '60vh',
        overflow: 'auto',
    },
})

const DEFAULT_COLORS = [
    '#8764B8', '#0078D4', '#00B7C3', '#498205', '#D13438', '#8A8886',
    '#E3008C', '#CA5010', '#107C10', '#005B70', '#004E8C', '#7719AA',
]

interface EditState {
    id: number | null // null = new level
    name: string
    iconName: string
    color: string
    ordinal: number
}

interface ManageLevelsDialogProps {
    projectId: string
    open: boolean
    onOpenChange: (open: boolean) => void
}

export function ManageLevelsDialog({ projectId, open, onOpenChange }: ManageLevelsDialogProps) {
    const styles = useStyles()
    const { data: levels } = useWorkItemLevels(projectId)
    const createMutation = useCreateWorkItemLevel(projectId)
    const updateMutation = useUpdateWorkItemLevel(projectId)
    const deleteMutation = useDeleteWorkItemLevel(projectId)

    const [editState, setEditState] = useState<EditState | null>(null)

    const sorted = useMemo(() =>
        [...(levels ?? [])].sort((a, b) => a.ordinal - b.ordinal),
        [levels],
    )

    // Reset edit state when dialog closes
    useEffect(() => {
        if (!open) setEditState(null)
    }, [open])

    const handleStartEdit = useCallback((level: WorkItemLevel) => {
        setEditState({
            id: level.id,
            name: level.name,
            iconName: level.iconName,
            color: level.color,
            ordinal: level.ordinal,
        })
    }, [])

    const handleStartNew = useCallback(() => {
        setEditState({
            id: null,
            name: '',
            iconName: 'circle',
            color: '#0078D4',
            ordinal: sorted.length,
        })
    }, [sorted.length])

    const handleCancelEdit = useCallback(() => {
        setEditState(null)
    }, [])

    const handleSave = useCallback(() => {
        if (!editState || !editState.name.trim()) return

        if (editState.id === null) {
            // Create new
            createMutation.mutate(
                {
                    name: editState.name.trim(),
                    iconName: editState.iconName,
                    color: editState.color,
                    ordinal: editState.ordinal,
                },
                { onSuccess: () => setEditState(null) },
            )
        } else {
            // Update existing
            updateMutation.mutate(
                {
                    id: editState.id,
                    data: {
                        name: editState.name.trim(),
                        iconName: editState.iconName,
                        color: editState.color,
                        ordinal: editState.ordinal,
                    },
                },
                { onSuccess: () => setEditState(null) },
            )
        }
    }, [editState, createMutation, updateMutation])

    const handleDelete = useCallback((id: number) => {
        deleteMutation.mutate(id)
    }, [deleteMutation])

    const handleMoveUp = useCallback((level: WorkItemLevel) => {
        const idx = sorted.findIndex((l) => l.id === level.id)
        if (idx <= 0) return
        const prev = sorted[idx - 1]
        updateMutation.mutate({ id: level.id, data: { ordinal: prev.ordinal } })
        updateMutation.mutate({ id: prev.id, data: { ordinal: level.ordinal } })
    }, [sorted, updateMutation])

    const handleMoveDown = useCallback((level: WorkItemLevel) => {
        const idx = sorted.findIndex((l) => l.id === level.id)
        if (idx < 0 || idx >= sorted.length - 1) return
        const next = sorted[idx + 1]
        updateMutation.mutate({ id: level.id, data: { ordinal: next.ordinal } })
        updateMutation.mutate({ id: next.id, data: { ordinal: level.ordinal } })
    }, [sorted, updateMutation])

    const isBusy = createMutation.isPending || updateMutation.isPending || deleteMutation.isPending

    return (
        <Dialog open={open} onOpenChange={(_e, data) => onOpenChange(data.open)}>
            <DialogSurface>
                <DialogBody>
                    <DialogTitle>Manage Work Item Levels</DialogTitle>
                    <DialogContent>
                        <div className={styles.dialogContent}>
                            <Caption1>
                                Configure levels for your work item hierarchy. Drag to reorder, or use the arrow buttons.
                            </Caption1>

                            {sorted.length === 0 && !editState && (
                                <div className={styles.emptyState}>
                                    <Text>No levels configured</Text>
                                    <Caption1>Add levels to categorize your work items</Caption1>
                                </div>
                            )}

                            <div className={styles.levelList}>
                                {sorted.map((level, idx) =>
                                    editState?.id === level.id ? (
                                        <LevelEditForm
                                            key={level.id}
                                            editState={editState}
                                            setEditState={setEditState}
                                            onSave={handleSave}
                                            onCancel={handleCancelEdit}
                                            isBusy={isBusy}
                                            styles={styles}
                                        />
                                    ) : (
                                        <div key={level.id} className={styles.levelRow}>
                                            <span className={styles.levelIcon} style={{ color: level.color }}>
                                                {resolveLevelIcon(level.iconName)}
                                            </span>
                                            <div className={styles.levelPreview}>
                                                <Text weight="semibold" size={200}>{level.name}</Text>
                                                {level.isDefault && (
                                                    <Caption1>(default)</Caption1>
                                                )}
                                            </div>
                                            <div className={styles.levelActions}>
                                                <Tooltip content="Move up" relationship="label">
                                                    <Button
                                                        appearance="subtle"
                                                        size="small"
                                                        icon={<ArrowUpRegular />}
                                                        disabled={idx === 0 || isBusy}
                                                        onClick={() => handleMoveUp(level)}
                                                        aria-label="Move up"
                                                    />
                                                </Tooltip>
                                                <Tooltip content="Move down" relationship="label">
                                                    <Button
                                                        appearance="subtle"
                                                        size="small"
                                                        icon={<ArrowDownRegular />}
                                                        disabled={idx === sorted.length - 1 || isBusy}
                                                        onClick={() => handleMoveDown(level)}
                                                        aria-label="Move down"
                                                    />
                                                </Tooltip>
                                                <Tooltip content="Edit" relationship="label">
                                                    <Button
                                                        appearance="subtle"
                                                        size="small"
                                                        icon={<EditRegular />}
                                                        disabled={isBusy}
                                                        onClick={() => handleStartEdit(level)}
                                                        aria-label="Edit"
                                                    />
                                                </Tooltip>
                                                <Tooltip content="Delete" relationship="label">
                                                    <Button
                                                        appearance="subtle"
                                                        size="small"
                                                        icon={<DeleteRegular />}
                                                        disabled={isBusy}
                                                        onClick={() => handleDelete(level.id)}
                                                        aria-label="Delete"
                                                    />
                                                </Tooltip>
                                            </div>
                                        </div>
                                    ),
                                )}

                                {editState?.id === null && (
                                    <LevelEditForm
                                        editState={editState}
                                        setEditState={setEditState}
                                        onSave={handleSave}
                                        onCancel={handleCancelEdit}
                                        isBusy={isBusy}
                                        styles={styles}
                                    />
                                )}
                            </div>

                            {!editState && (
                                <Button
                                    appearance="subtle"
                                    icon={<AddRegular />}
                                    onClick={handleStartNew}
                                    disabled={isBusy}
                                >
                                    Add Level
                                </Button>
                            )}
                        </div>
                    </DialogContent>
                    <DialogActions>
                        <Button appearance="secondary" onClick={() => onOpenChange(false)}>
                            Close
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    )
}

interface LevelEditFormProps {
    editState: EditState
    setEditState: (state: EditState | null) => void
    onSave: () => void
    onCancel: () => void
    isBusy: boolean
    styles: ReturnType<typeof useStyles>
}

function LevelEditForm({ editState, setEditState, onSave, onCancel, isBusy, styles }: LevelEditFormProps) {
    return (
        <div className={styles.editForm}>
            <Field label="Name" required>
                <Input
                    value={editState.name}
                    onChange={(_e, data) => setEditState({ ...editState, name: data.value })}
                    placeholder="Level name"
                />
            </Field>
            <div className={styles.editFormRow}>
                <Field label="Icon">
                    <Dropdown
                        value={editState.iconName}
                        onOptionSelect={(_e, data) => setEditState({ ...editState, iconName: data.optionValue ?? 'circle' })}
                    >
                        {LEVEL_ICON_NAMES.map((iconName) => (
                            <Option key={iconName} value={iconName} text={iconName}>
                                <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.375rem' }}>
                                    <span style={{ display: 'flex', alignItems: 'center', color: editState.color }}>
                                        {resolveLevelIcon(iconName)}
                                    </span>
                                    {iconName}
                                </span>
                            </Option>
                        ))}
                    </Dropdown>
                </Field>
                <Field label="Color">
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: '4px', paddingTop: '4px' }}>
                        {DEFAULT_COLORS.map((c) => (
                            <Tooltip key={c} content={c} relationship="label">
                                <div
                                    className={styles.colorPreview}
                                    style={{
                                        backgroundColor: c,
                                        outline: editState.color === c ? `2px solid ${tokens.colorBrandStroke1}` : undefined,
                                        outlineOffset: '1px',
                                    }}
                                    onClick={() => setEditState({ ...editState, color: c })}
                                />
                            </Tooltip>
                        ))}
                        <input
                            type="color"
                            value={editState.color}
                            onChange={(e) => setEditState({ ...editState, color: e.target.value })}
                            style={{ width: '32px', height: '28px', padding: 0, border: 'none', cursor: 'pointer' }}
                        />
                    </div>
                </Field>
            </div>
            <div style={{ display: 'flex', gap: '0.5rem', justifyContent: 'flex-end' }}>
                <Button
                    appearance="subtle"
                    icon={<DismissRegular />}
                    onClick={onCancel}
                    disabled={isBusy}
                    size="small"
                >
                    Cancel
                </Button>
                <Button
                    appearance="primary"
                    icon={<SaveRegular />}
                    onClick={onSave}
                    disabled={!editState.name.trim() || isBusy}
                    size="small"
                >
                    {editState.id === null ? 'Add' : 'Save'}
                </Button>
            </div>
        </div>
    )
}
