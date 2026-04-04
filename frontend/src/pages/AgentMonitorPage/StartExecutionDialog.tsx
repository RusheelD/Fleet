import { useEffect, useMemo, useState } from 'react'
import {
    makeStyles,
    mergeClasses,
    Dialog,
    DialogSurface,
    DialogTitle,
    DialogBody,
    DialogContent,
    DialogActions,
    DialogTrigger,
    Button,
    Spinner,
    Text,
    Caption1,
    Badge,
    Radio,
    RadioGroup,
    Field,
    Input,
} from '@fluentui/react-components'
import { BotRegular } from '@fluentui/react-icons'
import type { WorkItem } from '../../models'
import { usePreferences, useIsMobile } from '../../hooks'
import { appTokens } from '../../styles/appTokens'
import { InfoBadge } from '../../components/shared/InfoBadge'
import { FleetRocketLogo } from '../../components/shared'

interface StartExecutionDialogProps {
    open: boolean
    onOpenChange: (open: boolean) => void
    workItems: WorkItem[]
    isLoading: boolean
    isPending: boolean
    onStart: (workItemNumber: number, targetBranch: string) => void
}

const useStyles = makeStyles({
    dialogSurface: {
        width: 'min(760px, calc(100vw - 1.5rem))',
    },
    dialogSurfaceMobile: {
        width: 'calc(100vw - 0.75rem)',
    },
    content: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.md,
    },
    contentCompact: {
        gap: appTokens.space.sm,
    },
    introCard: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxxs,
        paddingTop: appTokens.space.sm,
        paddingBottom: appTokens.space.sm,
        paddingLeft: appTokens.space.lg,
        paddingRight: appTokens.space.lg,
        borderRadius: appTokens.radius.md,
        backgroundColor: appTokens.color.surfaceAlt,
        border: appTokens.border.subtle,
    },
    sectionHeader: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: appTokens.space.sm,
    },
    workItemList: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xs,
        maxHeight: '400px',
        overflow: 'auto',
    },
    workItemListCompact: {
        gap: '4px',
        maxHeight: '330px',
    },
    workItemRadio: {
        margin: 0,
        border: appTokens.border.subtle,
        borderRadius: appTokens.radius.md,
        backgroundColor: appTokens.color.surface,
        paddingTop: appTokens.space.xs,
        paddingBottom: appTokens.space.xs,
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
        ':hover': {
            backgroundColor: appTokens.color.surfaceHover,
        },
        '& .fui-Radio__indicator': {
            backgroundColor: appTokens.color.surface,
        },
        '& input:checked ~ .fui-Radio__indicator': {
            borderTopColor: appTokens.color.brand,
            borderRightColor: appTokens.color.brand,
            borderBottomColor: appTokens.color.brand,
            borderLeftColor: appTokens.color.brand,
            color: appTokens.color.brand,
            backgroundColor: appTokens.color.surface,
        },
        '& input:checked:hover ~ .fui-Radio__indicator': {
            borderTopColor: appTokens.color.brandHover,
            borderRightColor: appTokens.color.brandHover,
            borderBottomColor: appTokens.color.brandHover,
            borderLeftColor: appTokens.color.brandHover,
            color: appTokens.color.brandHover,
        },
    },
    workItemRadioSelected: {
        backgroundColor: appTokens.color.surfaceBrand,
        boxShadow: appTokens.border.activeInset,
    },
    workItemRadioCompact: {
        paddingTop: appTokens.space.xxs,
        paddingBottom: appTokens.space.xxs,
    },
    radioLabel: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxxs,
        width: '100%',
    },
    workItemTopRow: {
        display: 'flex',
        alignItems: 'flex-start',
        justifyContent: 'space-between',
        gap: appTokens.space.sm,
        width: '100%',
    },
    workItemTopRowCompact: {
        gap: appTokens.space.xs,
    },
    workItemTopRowMobile: {
        flexDirection: 'column',
        alignItems: 'flex-start',
        gap: appTokens.space.xxxs,
    },
    workItemTitleWrap: {
        display: 'flex',
        alignItems: 'baseline',
        gap: appTokens.space.sm,
        minWidth: 0,
    },
    workItemTitleWrapCompact: {
        gap: appTokens.space.xs,
    },
    workItemNumber: {
        fontWeight: appTokens.fontWeight.semibold,
        color: appTokens.color.brand,
        whiteSpace: 'nowrap',
    },
    workItemTitle: {
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    workItemTitleMobile: {
        whiteSpace: 'normal',
        overflow: 'visible',
        textOverflow: 'clip',
    },
    workItemMeta: {
        color: appTokens.color.textTertiary,
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.xs,
        flexWrap: 'wrap',
        marginLeft: `calc(${appTokens.space.xxxs} + ${appTokens.fontSize.iconMd})`,
    },
    dot: {
        color: appTokens.color.textMuted,
    },
    loadingState: {
        paddingTop: appTokens.space.xl,
        paddingBottom: appTokens.space.xl,
        display: 'flex',
        justifyContent: 'center',
    },
    footerHint: {
        color: appTokens.color.textTertiary,
    },
    branchField: {
        maxWidth: '320px',
    },
    branchFieldMobile: {
        maxWidth: 'unset',
        width: '100%',
    },
    emptyState: {
        padding: '2rem 1.5rem',
        textAlign: 'center' as const,
    },
    dialogActionsMobile: {
        width: '100%',
        justifyContent: 'stretch',
        display: 'grid',
        gap: appTokens.space.xs,
    },
    dialogActionButtonMobile: {
        width: '100%',
    },
})

function getStateBadgeTone(state: WorkItem['state']): 'brand' | 'info' | 'warning' | 'success' {
    if (state === 'Planning (AI)' || state === 'In Progress' || state === 'In Progress (AI)') {
        return 'warning'
    }

    if (state === 'Active') {
        return 'success'
    }

    if (state === 'New') {
        return 'brand'
    }

    return 'info'
}

function formatStateLabel(state: WorkItem['state']): string {
    if (state === 'In-PR') return 'In PR'
    if (state === 'In-PR (AI)') return 'In PR (AI)'
    return state
}

export function StartExecutionDialog({
    open,
    onOpenChange,
    workItems,
    isLoading,
    isPending,
    onStart,
}: StartExecutionDialogProps) {
    const styles = useStyles()
    const isMobile = useIsMobile()
    const { preferences } = usePreferences()
    const isCompact = preferences?.compactMode ?? false
    const [selected, setSelected] = useState<number | null>(null)
    const [targetBranch, setTargetBranch] = useState('main')

    // Filter to work items that are eligible (AI-assigned, active-ish states)
    const eligible = useMemo(
        () =>
            workItems.filter(
                (wi) =>
                    wi.isAI &&
                    !['Closed', 'Resolved', 'Resolved (AI)', 'In-PR', 'In-PR (AI)'].includes(wi.state),
            ),
        [workItems],
    )

    useEffect(() => {
        if (!open) {
            setSelected(null)
            setTargetBranch('main')
            return
        }

        setSelected((current) => {
            if (eligible.length === 0) return null
            if (current !== null && eligible.some((wi) => wi.workItemNumber === current)) return current
            return eligible[0].workItemNumber
        })
    }, [open, eligible])

    const handleStart = () => {
        const normalizedTargetBranch = targetBranch.trim()
        if (selected !== null && normalizedTargetBranch.length > 0) {
            onStart(selected, normalizedTargetBranch)
        }
    }

    return (
        <Dialog open={open} onOpenChange={(_e, data) => onOpenChange(data.open)}>
            <DialogSurface className={mergeClasses(styles.dialogSurface, isMobile && styles.dialogSurfaceMobile)}>
                <DialogBody>
                    <DialogTitle>Start Agent Execution</DialogTitle>
                    <DialogContent>
                        {isLoading ? (
                            <div className={styles.loadingState}>
                                <Spinner label="Loading work items..." />
                            </div>
                        ) : eligible.length === 0 ? (
                            <div className={styles.emptyState}>
                                <Text>
                                    No AI-assigned work items available. Create work items and assign them to Fleet AI first.
                                </Text>
                            </div>
                        ) : (
                            <div className={mergeClasses(styles.content, isCompact && styles.contentCompact)}>
                                <div className={styles.introCard}>
                                    <Text weight="semibold">Pick one AI work item to execute.</Text>
                                    <Caption1>
                                        Fleet runs the full agent pipeline by default unless the work item uses a manual agent cap.
                                    </Caption1>
                                </div>
                                <Field
                                    label="PR Target Branch"
                                    hint="Agents will open the draft PR against this branch."
                                    className={mergeClasses(styles.branchField, isMobile && styles.branchFieldMobile)}
                                    required
                                >
                                    <Input
                                        value={targetBranch}
                                        onChange={(_e, data) => setTargetBranch(data.value)}
                                        placeholder="main"
                                        disabled={isPending}
                                    />
                                </Field>
                                <div className={styles.sectionHeader}>
                                    <Text weight="semibold">Eligible Work Items</Text>
                                    <InfoBadge appearance="filled" size="small">
                                        {eligible.length} available
                                    </InfoBadge>
                                </div>
                                <RadioGroup
                                    value={selected?.toString() ?? ''}
                                    onChange={(_e, data) => setSelected(Number(data.value))}
                                >
                                    <div className={mergeClasses(styles.workItemList, isCompact && styles.workItemListCompact)}>
                                        {eligible.map((wi) => (
                                            (() => {
                                                const badgeTone = getStateBadgeTone(wi.state)

                                                return (
                                                    <Radio
                                                        key={wi.workItemNumber}
                                                        className={mergeClasses(
                                                            styles.workItemRadio,
                                                            selected === wi.workItemNumber && styles.workItemRadioSelected,
                                                            isCompact && styles.workItemRadioCompact,
                                                        )}
                                                        value={wi.workItemNumber.toString()}
                                                        label={
                                                            <span className={styles.radioLabel}>
                                                                <span className={mergeClasses(styles.workItemTopRow, isCompact && styles.workItemTopRowCompact, isMobile && styles.workItemTopRowMobile)}>
                                                                    <span className={mergeClasses(styles.workItemTitleWrap, isCompact && styles.workItemTitleWrapCompact)}>
                                                                        <span className={styles.workItemNumber}>
                                                                            #{wi.workItemNumber}
                                                                        </span>
                                                                        <Text weight="semibold" className={mergeClasses(styles.workItemTitle, isMobile && styles.workItemTitleMobile)}>
                                                                            {wi.title}
                                                                        </Text>
                                                                    </span>
                                                                    {badgeTone === 'info' ? (
                                                                        <InfoBadge appearance="filled" size="small">
                                                                            {formatStateLabel(wi.state)}
                                                                        </InfoBadge>
                                                                    ) : (
                                                                        <Badge appearance="filled" size="small" color={badgeTone}>
                                                                            {formatStateLabel(wi.state)}
                                                                        </Badge>
                                                                    )}
                                                                </span>
                                                                <span className={styles.workItemMeta}>
                                                                    <Caption1>Priority P{wi.priority}</Caption1>
                                                                    <span className={styles.dot}>|</span>
                                                                    <Caption1>Difficulty D{wi.difficulty}</Caption1>
                                                                    {wi.assignmentMode === 'manual' && wi.assignedAgentCount ? (
                                                                        <>
                                                                            <span className={styles.dot}>|</span>
                                                                            <Caption1>{wi.assignedAgentCount} agents</Caption1>
                                                                        </>
                                                                    ) : (
                                                                        <>
                                                                            <span className={styles.dot}>|</span>
                                                                            <Caption1>Unlimited agents</Caption1>
                                                                        </>
                                                                    )}
                                                                </span>
                                                            </span>
                                                        }
                                                    />
                                                )
                                            })()
                                        ))}
                                    </div>
                                </RadioGroup>
                                <Caption1 className={styles.footerHint}>
                                    <BotRegular /> Only AI-assigned work items are shown in this list.
                                </Caption1>
                            </div>
                        )}
                    </DialogContent>
                    <DialogActions className={mergeClasses(isMobile && styles.dialogActionsMobile)}>
                        <DialogTrigger disableButtonEnhancement>
                            <Button appearance="secondary" className={mergeClasses(isMobile && styles.dialogActionButtonMobile)}>Cancel</Button>
                        </DialogTrigger>
                        <Button
                            appearance="primary"
                            icon={<FleetRocketLogo size={18} title="Start execution" variant="outline" />}
                            disabled={selected === null || targetBranch.trim().length === 0 || isPending}
                            onClick={handleStart}
                            className={mergeClasses(isMobile && styles.dialogActionButtonMobile)}
                        >
                            {isPending ? 'Starting...' : 'Start Execution'}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    )
}
