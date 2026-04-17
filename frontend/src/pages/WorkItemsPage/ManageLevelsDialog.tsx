import { useState, useMemo, useCallback, useEffect } from 'react'
import {
    makeStyles,
    Button,
    Dialog,
    DialogSurface,
    DialogTitle,
    DialogBody,
    DialogContent,
    DialogActions,
    Text,
    Caption1,
} from '@fluentui/react-components'
import { AddRegular } from '@fluentui/react-icons'
import {
    useWorkItemLevels,
    useCreateWorkItemLevel,
    useUpdateWorkItemLevel,
    useDeleteWorkItemLevel,
} from '../../proxies'
import type { WorkItemLevel } from '../../models'
import { APP_MOBILE_MEDIA_QUERY, appTokens } from '../../styles/appTokens'
import type { EditState } from './workItemLevelEditorTypes'
import { WorkItemLevelEditForm } from './WorkItemLevelEditForm'
import { WorkItemLevelRow } from './WorkItemLevelRow'

const useStyles = makeStyles({
    dialogSurface: {
        width: 'min(760px, calc(100vw - 1rem))',
        maxHeight: 'calc(100vh - 1rem)',
    },
    levelList: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxs,
    },
    levelRow: {
        display: 'grid',
        gridTemplateColumns: '32px 1fr auto',
        alignItems: 'center',
        gap: appTokens.space.sm,
        paddingTop: appTokens.space.xs,
        paddingBottom: appTokens.space.xs,
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
        borderRadius: appTokens.radius.md,
        [APP_MOBILE_MEDIA_QUERY]: {
            gridTemplateColumns: '24px 1fr',
            rowGap: appTokens.space.xs,
        },
        ':hover': {
            backgroundColor: appTokens.color.surfaceHover,
        },
    },
    levelPreview: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.sm,
    },
    levelIcon: {
        display: 'flex',
        alignItems: 'center',
        fontSize: appTokens.fontSize.iconSm,
    },
    levelMeta: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.xxs,
    },
    levelActions: {
        display: 'flex',
        alignItems: 'center',
        gap: '2px',
        [APP_MOBILE_MEDIA_QUERY]: {
            gridColumnStart: 1,
            gridColumnEnd: 3,
            justifyContent: 'flex-end',
            flexWrap: 'wrap',
        },
    },
    editForm: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.md,
        padding: appTokens.space.md,
        borderRadius: appTokens.radius.md,
        backgroundColor: appTokens.color.pageBackground,
    },
    editFormRow: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: appTokens.space.md,
        [APP_MOBILE_MEDIA_QUERY]: {
            gridTemplateColumns: '1fr',
            gap: appTokens.space.sm,
        },
    },
    colorPreview: {
        width: '24px',
        height: '24px',
        borderRadius: '4px',
        borderTopWidth: '1px',
        borderRightWidth: '1px',
        borderBottomWidth: '1px',
        borderLeftWidth: '1px',
        borderTopStyle: 'solid',
        borderRightStyle: 'solid',
        borderBottomStyle: 'solid',
        borderLeftStyle: 'solid',
        borderTopColor: appTokens.color.borderSubtle,
        borderRightColor: appTokens.color.borderSubtle,
        borderBottomColor: appTokens.color.borderSubtle,
        borderLeftColor: appTokens.color.borderSubtle,
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
        borderRadius: appTokens.radius.md,
        cursor: 'pointer',
        fontSize: appTokens.fontSize.sm,
        ':hover': {
            backgroundColor: appTokens.color.surfaceHover,
        },
    },
    iconOptionSelected: {
        width: '28px',
        height: '28px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        borderRadius: appTokens.radius.md,
        cursor: 'pointer',
        fontSize: appTokens.fontSize.sm,
        backgroundColor: appTokens.color.surfaceBrand,
        outline: `2px solid ${appTokens.color.brandStroke}`,
    },
    emptyState: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: appTokens.space.sm,
        padding: appTokens.space.xl,
        color: appTokens.color.textTertiary,
    },
    dialogContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.md,
        maxHeight: '60vh',
        overflow: 'auto',
    },
    editActions: {
        display: 'flex',
        gap: appTokens.space.sm,
        justifyContent: 'flex-end',
        [APP_MOBILE_MEDIA_QUERY]: {
            flexDirection: 'column',
            alignItems: 'stretch',
        },
    },
    editActionButtonMobile: {
        [APP_MOBILE_MEDIA_QUERY]: {
            width: '100%',
        },
    },
})

const DEFAULT_COLORS = appTokens.palette.workItemLevels

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
            color: appTokens.color.workItemLevelDefault,
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
            <DialogSurface className={styles.dialogSurface}>
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
                                        <WorkItemLevelEditForm
                                            key={level.id}
                                            editState={editState}
                                            setEditState={setEditState}
                                            onSave={handleSave}
                                            onCancel={handleCancelEdit}
                                            isBusy={isBusy}
                                            defaultColors={DEFAULT_COLORS}
                                            editFormClassName={styles.editForm}
                                            editFormRowClassName={styles.editFormRow}
                                            colorPreviewClassName={styles.colorPreview}
                                            editActionsClassName={styles.editActions}
                                            editActionButtonMobileClassName={styles.editActionButtonMobile}
                                        />
                                    ) : (
                                        <WorkItemLevelRow
                                            key={level.id}
                                            level={level}
                                            index={idx}
                                            totalCount={sorted.length}
                                            isBusy={isBusy}
                                            levelRowClassName={styles.levelRow}
                                            levelIconClassName={styles.levelIcon}
                                            levelPreviewClassName={styles.levelPreview}
                                            levelActionsClassName={styles.levelActions}
                                            onMoveUp={handleMoveUp}
                                            onMoveDown={handleMoveDown}
                                            onEdit={handleStartEdit}
                                            onDelete={handleDelete}
                                        />
                                    ),
                                )}

                                {editState?.id === null && (
                                    <WorkItemLevelEditForm
                                        editState={editState}
                                        setEditState={setEditState}
                                        onSave={handleSave}
                                        onCancel={handleCancelEdit}
                                        isBusy={isBusy}
                                        defaultColors={DEFAULT_COLORS}
                                        editFormClassName={styles.editForm}
                                        editFormRowClassName={styles.editFormRow}
                                        colorPreviewClassName={styles.colorPreview}
                                        editActionsClassName={styles.editActions}
                                        editActionButtonMobileClassName={styles.editActionButtonMobile}
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
