import { useState } from 'react'
import {
    makeStyles,
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
    Badge,
    Radio,
    RadioGroup,
} from '@fluentui/react-components'
import { RocketRegular } from '@fluentui/react-icons'
import type { WorkItem } from '../../models'

interface StartExecutionDialogProps {
    open: boolean
    onOpenChange: (open: boolean) => void
    workItems: WorkItem[]
    isLoading: boolean
    isPending: boolean
    onStart: (workItemNumber: number) => void
}

const useStyles = makeStyles({
    workItemList: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0.25rem',
        maxHeight: '400px',
        overflow: 'auto',
    },
    radioLabel: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
    },
    workItemNumber: {
        fontWeight: tokens.fontWeightSemibold,
        minWidth: '40px',
    },
    emptyState: {
        padding: '2rem',
        textAlign: 'center' as const,
    },
})

export function StartExecutionDialog({
    open,
    onOpenChange,
    workItems,
    isLoading,
    isPending,
    onStart,
}: StartExecutionDialogProps) {
    const styles = useStyles()
    const [selected, setSelected] = useState<number | null>(null)

    // Filter to work items that are eligible (AI-assigned, active-ish states)
    const eligible = workItems.filter(
        (wi) =>
            wi.isAI &&
            !['Closed', 'Resolved', 'Resolved (AI)'].includes(wi.state),
    )

    const handleStart = () => {
        if (selected !== null) {
            onStart(selected)
        }
    }

    return (
        <Dialog open={open} onOpenChange={(_e, data) => onOpenChange(data.open)}>
            <DialogSurface>
                <DialogBody>
                    <DialogTitle>Start Agent Execution</DialogTitle>
                    <DialogContent>
                        {isLoading ? (
                            <Spinner label="Loading work items..." />
                        ) : eligible.length === 0 ? (
                            <div className={styles.emptyState}>
                                <Text>
                                    No AI-assigned work items available. Create work items and assign them to Fleet AI first.
                                </Text>
                            </div>
                        ) : (
                            <>
                                <Text block style={{ marginBottom: '0.75rem' }}>
                                    Select a work item to execute with agents:
                                </Text>
                                <RadioGroup
                                    value={selected?.toString() ?? ''}
                                    onChange={(_e, data) => setSelected(Number(data.value))}
                                >
                                    <div className={styles.workItemList}>
                                        {eligible.map((wi) => (
                                            <Radio
                                                key={wi.workItemNumber}
                                                value={wi.workItemNumber.toString()}
                                                label={
                                                    <span className={styles.radioLabel}>
                                                        <span className={styles.workItemNumber}>
                                                            #{wi.workItemNumber}
                                                        </span>
                                                        <span>{wi.title}</span>
                                                        <Badge
                                                            appearance="outline"
                                                            size="small"
                                                            color={
                                                                wi.state === 'Active' || wi.state === 'In Progress' || wi.state === 'In Progress (AI)'
                                                                    ? 'success'
                                                                    : 'informative'
                                                            }
                                                        >
                                                            {wi.state}
                                                        </Badge>
                                                    </span>
                                                }
                                            />
                                        ))}
                                    </div>
                                </RadioGroup>
                            </>
                        )}
                    </DialogContent>
                    <DialogActions>
                        <DialogTrigger disableButtonEnhancement>
                            <Button appearance="secondary">Cancel</Button>
                        </DialogTrigger>
                        <Button
                            appearance="primary"
                            icon={<RocketRegular />}
                            disabled={selected === null || isPending}
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
