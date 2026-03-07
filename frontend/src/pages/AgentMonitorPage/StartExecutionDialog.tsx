import { useEffect, useMemo, useState } from 'react'
import {
    makeStyles,
    mergeClasses,
    tokens,
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
import { BotRegular, RocketRegular } from '@fluentui/react-icons'
import type { WorkItem } from '../../models'
import { usePreferences } from '../../hooks'

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
    content: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
    },
    contentCompact: {
        gap: tokens.spacingVerticalS,
    },
    introCard: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
        paddingTop: tokens.spacingVerticalS,
        paddingBottom: tokens.spacingVerticalS,
        paddingLeft: tokens.spacingHorizontalM,
        paddingRight: tokens.spacingHorizontalM,
        borderRadius: tokens.borderRadiusMedium,
        backgroundColor: tokens.colorNeutralBackground2,
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
    sectionHeader: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: tokens.spacingHorizontalS,
    },
    workItemList: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
        maxHeight: '400px',
        overflow: 'auto',
    },
    workItemListCompact: {
        gap: '4px',
        maxHeight: '330px',
    },
    workItemRadio: {
        margin: 0,
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
        borderRadius: tokens.borderRadiusMedium,
        backgroundColor: tokens.colorNeutralBackground1,
        paddingTop: tokens.spacingVerticalXS,
        paddingBottom: tokens.spacingVerticalXS,
        paddingLeft: tokens.spacingHorizontalS,
        paddingRight: tokens.spacingHorizontalS,
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },
    workItemRadioSelected: {
        borderTopColor: tokens.colorBrandStroke1,
        borderRightColor: tokens.colorBrandStroke1,
        borderBottomColor: tokens.colorBrandStroke1,
        borderLeftColor: tokens.colorBrandStroke1,
        backgroundColor: tokens.colorBrandBackground2,
    },
    workItemRadioCompact: {
        paddingTop: '4px',
        paddingBottom: '4px',
    },
    radioLabel: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
        width: '100%',
    },
    workItemTopRow: {
        display: 'flex',
        alignItems: 'flex-start',
        justifyContent: 'space-between',
        gap: tokens.spacingHorizontalS,
        width: '100%',
    },
    workItemTopRowCompact: {
        gap: tokens.spacingHorizontalXS,
    },
    workItemTitleWrap: {
        display: 'flex',
        alignItems: 'baseline',
        gap: tokens.spacingHorizontalS,
        minWidth: 0,
    },
    workItemTitleWrapCompact: {
        gap: tokens.spacingHorizontalXS,
    },
    workItemNumber: {
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorBrandForeground1,
        whiteSpace: 'nowrap',
    },
    workItemTitle: {
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    workItemMeta: {
        color: tokens.colorNeutralForeground3,
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        flexWrap: 'wrap',
        marginLeft: `calc(${tokens.spacingHorizontalXXS} + 18px)`,
    },
    dot: {
        color: tokens.colorNeutralForeground4,
    },
    loadingState: {
        paddingTop: tokens.spacingVerticalXL,
        paddingBottom: tokens.spacingVerticalXL,
        display: 'flex',
        justifyContent: 'center',
    },
    footerHint: {
        color: tokens.colorNeutralForeground3,
    },
    branchField: {
        maxWidth: '320px',
    },
    emptyState: {
        padding: '2rem 1.5rem',
        textAlign: 'center' as const,
    },
})

function getStateBadgeColor(state: WorkItem['state']): 'brand' | 'informative' | 'warning' | 'success' {
    if (state === 'Planning (AI)' || state === 'In Progress' || state === 'In Progress (AI)') {
        return 'warning'
    }

    if (state === 'Active') {
        return 'success'
    }

    if (state === 'New') {
        return 'brand'
    }

    return 'informative'
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
            <DialogSurface className={styles.dialogSurface}>
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
                                        Fleet will run the assigned agents and update progress automatically.
                                    </Caption1>
                                </div>
                                <Field
                                    label="PR Target Branch"
                                    hint="Agents will open the draft PR against this branch."
                                    className={styles.branchField}
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
                                    <Badge appearance="filled" color="informative" size="small">
                                        {eligible.length} available
                                    </Badge>
                                </div>
                                <RadioGroup
                                    value={selected?.toString() ?? ''}
                                    onChange={(_e, data) => setSelected(Number(data.value))}
                                >
                                    <div className={mergeClasses(styles.workItemList, isCompact && styles.workItemListCompact)}>
                                        {eligible.map((wi) => (
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
                                                        <span className={mergeClasses(styles.workItemTopRow, isCompact && styles.workItemTopRowCompact)}>
                                                            <span className={mergeClasses(styles.workItemTitleWrap, isCompact && styles.workItemTitleWrapCompact)}>
                                                                <span className={styles.workItemNumber}>
                                                                    #{wi.workItemNumber}
                                                                </span>
                                                                <Text weight="semibold" className={styles.workItemTitle}>
                                                                    {wi.title}
                                                                </Text>
                                                            </span>
                                                            <Badge appearance="filled" size="small" color={getStateBadgeColor(wi.state)}>
                                                                {formatStateLabel(wi.state)}
                                                            </Badge>
                                                        </span>
                                                        <span className={styles.workItemMeta}>
                                                            <Caption1>Priority P{wi.priority}</Caption1>
                                                            <span className={styles.dot}>|</span>
                                                            <Caption1>Difficulty D{wi.difficulty}</Caption1>
                                                            {wi.assignedAgentCount ? (
                                                                <>
                                                                    <span className={styles.dot}>|</span>
                                                                    <Caption1>{wi.assignedAgentCount} agents</Caption1>
                                                                </>
                                                            ) : (
                                                                <>
                                                                    <span className={styles.dot}>|</span>
                                                                    <Caption1>Auto agents</Caption1>
                                                                </>
                                                            )}
                                                        </span>
                                                    </span>
                                                }
                                            />
                                        ))}
                                    </div>
                                </RadioGroup>
                                <Caption1 className={styles.footerHint}>
                                    <BotRegular /> Only AI-assigned work items are shown in this list.
                                </Caption1>
                            </div>
                        )}
                    </DialogContent>
                    <DialogActions>
                        <DialogTrigger disableButtonEnhancement>
                            <Button appearance="secondary">Cancel</Button>
                        </DialogTrigger>
                        <Button
                            appearance="primary"
                            icon={<RocketRegular />}
                            disabled={selected === null || targetBranch.trim().length === 0 || isPending}
                            onClick={handleStart}
                        >
                            {isPending ? 'Starting...' : 'Start Execution'}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    )
}
