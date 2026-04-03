import { useState } from 'react'
import {
    makeStyles,
    mergeClasses,
    Button,
    Input,
    Dropdown,
    Option,
    Textarea,
    Field,
    Dialog,
    DialogTrigger,
    DialogSurface,
    DialogTitle,
    DialogBody,
    DialogContent,
    DialogActions,
} from '@fluentui/react-components'
import { useCreateWorkItem } from '../../proxies'
import { resolveLevelIcon } from '../../proxies'
import type { WorkItem, WorkItemLevel } from '../../models'
import { useIsMobile } from '../../hooks'
import {
    AUTO_ASSIGNMENT_LABEL,
    WORK_ITEM_ASSIGNMENT_OPTION_LABELS,
    getWorkItemAssignmentSettings,
} from './workItemAssignmentOptions'

const PRIORITY_MAP: Record<string, number> = {
    'P1 - Critical': 1,
    'P2 - High': 2,
    'P3 - Medium': 3,
    'P4 - Low': 4,
}

const DIFFICULTY_MAP: Record<string, number> = {
    '1 - Trivial': 1,
    '2 - Easy': 2,
    '3 - Moderate': 3,
    '4 - Hard': 4,
    '5 - Complex': 5,
}

const NONE_PARENT = '(None)'
const NONE_LEVEL = '(None)'

const useStyles = makeStyles({
    dialogSurface: {
        width: 'min(780px, calc(100vw - 1rem))',
        maxHeight: 'calc(100vh - 1rem)',
    },
    dialogForm: {
        display: 'flex',
        flexDirection: 'column',
        gap: '1rem',
    },
    dialogFormMobile: {
        gap: '0.75rem',
    },
    dialogFormGrid: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: '0.75rem',
    },
    dialogFormGridMobile: {
        gridTemplateColumns: '1fr',
        gap: '0.5rem',
    },
    dialogActionsMobile: {
        width: '100%',
        justifyContent: 'stretch',
        display: 'grid',
        gap: '0.5rem',
    },
    dialogActionButtonMobile: {
        width: '100%',
    },
})

interface CreateWorkItemDialogProps {
    projectId: string
    workItems?: WorkItem[]
    levels?: WorkItemLevel[]
    open: boolean
    onOpenChange: (open: boolean) => void
}

export function CreateWorkItemDialog({ projectId, workItems, levels, open, onOpenChange }: CreateWorkItemDialogProps) {
    const styles = useStyles()
    const isMobile = useIsMobile()
    const createMutation = useCreateWorkItem(projectId)

    const [title, setTitle] = useState('')
    const [description, setDescription] = useState('')
    const [acceptanceCriteria, setAcceptanceCriteria] = useState('')
    const [priorityLabel, setPriorityLabel] = useState('P2 - High')
    const [difficultyLabel, setDifficultyLabel] = useState('3 - Moderate')
    const [state, setState] = useState('New')
    const [tags, setTags] = useState('')
    const [agentLabel, setAgentLabel] = useState(AUTO_ASSIGNMENT_LABEL)
    const [parentLabel, setParentLabel] = useState(NONE_PARENT)
    const [levelLabel, setLevelLabel] = useState(NONE_LEVEL)

    const parentOptions = workItems ?? []
    const sortedLevels = [...(levels ?? [])].sort((a, b) => a.ordinal - b.ordinal)

    const resetForm = () => {
        setTitle('')
        setDescription('')
        setAcceptanceCriteria('')
        setPriorityLabel('P2 - High')
        setDifficultyLabel('3 - Moderate')
        setState('New')
        setTags('')
        setAgentLabel(AUTO_ASSIGNMENT_LABEL)
        setParentLabel(NONE_PARENT)
        setLevelLabel(NONE_LEVEL)
    }

    const handleCreate = () => {
        if (!title.trim()) return

        const selectedParent = parentOptions.find((wi) => `#${wi.workItemNumber} ${wi.title}` === parentLabel)
        const selectedLevel = sortedLevels.find((l) => l.name === levelLabel)
        const agentSettings = getWorkItemAssignmentSettings(agentLabel)

        createMutation.mutate(
            {
                title: title.trim(),
                description: description.trim(),
                priority: PRIORITY_MAP[priorityLabel] ?? 2,
                difficulty: DIFFICULTY_MAP[difficultyLabel] ?? 3,
                state,
                assignedTo: agentSettings.assignedTo,
                tags: tags
                    .split(',')
                    .map((t) => t.trim())
                    .filter(Boolean),
                isAI: agentSettings.isAI,
                parentWorkItemNumber: selectedParent?.workItemNumber ?? null,
                levelId: selectedLevel?.id ?? null,
                assignmentMode: agentSettings.assignmentMode,
                assignedAgentCount: agentSettings.assignedAgentCount,
                acceptanceCriteria: acceptanceCriteria.trim(),
            },
            {
                onSuccess: () => {
                    resetForm()
                    onOpenChange(false)
                },
            },
        )
    }

    return (
        <Dialog open={open} onOpenChange={(_e, data) => { onOpenChange(data.open); if (!data.open) resetForm() }}>
            <DialogSurface className={styles.dialogSurface}>
                <DialogBody>
                    <DialogTitle>Create Work Item</DialogTitle>
                    <DialogContent>
                        <div className={mergeClasses(styles.dialogForm, isMobile && styles.dialogFormMobile)}>
                            <Field label="Title" required>
                                <Input
                                    placeholder="Enter work item title"
                                    value={title}
                                    onChange={(_e, data) => setTitle(data.value)}
                                />
                            </Field>
                            <Field label="Description">
                                <Textarea
                                    placeholder="Describe the work item..."
                                    resize="vertical"
                                    rows={4}
                                    value={description}
                                    onChange={(_e, data) => setDescription(data.value)}
                                />
                            </Field>
                            <Field label="Acceptance Criteria">
                                <Textarea
                                    placeholder="Define what must be true for this work item to be done..."
                                    resize="vertical"
                                    rows={3}
                                    value={acceptanceCriteria}
                                    onChange={(_e, data) => setAcceptanceCriteria(data.value)}
                                />
                            </Field>
                            <div className={mergeClasses(styles.dialogFormGrid, isMobile && styles.dialogFormGridMobile)}>
                                <Field label="Level">
                                    <Dropdown
                                        placeholder="Select level"
                                        value={levelLabel}
                                        onOptionSelect={(_e, data) => setLevelLabel(data.optionText ?? NONE_LEVEL)}
                                    >
                                        <Option>{NONE_LEVEL}</Option>
                                        {sortedLevels.map((l) => (
                                            <Option key={l.id} text={l.name}>
                                                <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.375rem' }}>
                                                    <span style={{ color: l.color, display: 'flex', alignItems: 'center' }}>{resolveLevelIcon(l.iconName)}</span>
                                                    {l.name}
                                                </span>
                                            </Option>
                                        ))}
                                    </Dropdown>
                                </Field>
                                <Field label="Priority">
                                    <Dropdown
                                        placeholder="Select priority"
                                        value={priorityLabel}
                                        onOptionSelect={(_e, data) => setPriorityLabel(data.optionText ?? 'P2 - High')}
                                    >
                                        <Option>P1 - Critical</Option>
                                        <Option>P2 - High</Option>
                                        <Option>P3 - Medium</Option>
                                        <Option>P4 - Low</Option>
                                    </Dropdown>
                                </Field>
                                <Field label="Difficulty">
                                    <Dropdown
                                        placeholder="Select difficulty"
                                        value={difficultyLabel}
                                        onOptionSelect={(_e, data) => setDifficultyLabel(data.optionText ?? '3 - Moderate')}
                                    >
                                        <Option>1 - Trivial</Option>
                                        <Option>2 - Easy</Option>
                                        <Option>3 - Moderate</Option>
                                        <Option>4 - Hard</Option>
                                        <Option>5 - Complex</Option>
                                    </Dropdown>
                                </Field>
                                <Field label="State">
                                    <Dropdown
                                        placeholder="Select state"
                                        value={state}
                                        onOptionSelect={(_e, data) => setState(data.optionText ?? 'New')}
                                    >
                                        <Option>New</Option>
                                        <Option>Active</Option>
                                        <Option>In Progress</Option>
                                        <Option>In-PR</Option>
                                        <Option>In-PR (AI)</Option>
                                        <Option>Resolved</Option>
                                        <Option>Resolved (AI)</Option>
                                        <Option>Closed</Option>
                                    </Dropdown>
                                </Field>
                            </div>
                            <Field label="Tags">
                                <Input
                                    placeholder="e.g. frontend, backend, api"
                                    value={tags}
                                    onChange={(_e, data) => setTags(data.value)}
                                />
                            </Field>
                            <Field label="Parent Work Item">
                                <Dropdown
                                    placeholder="Select parent (optional)"
                                    value={parentLabel}
                                    onOptionSelect={(_e, data) => setParentLabel(data.optionText ?? NONE_PARENT)}
                                >
                                    <Option>{NONE_PARENT}</Option>
                                    {parentOptions.map((wi) => (
                                        <Option key={wi.workItemNumber}>{`#${wi.workItemNumber} ${wi.title}`}</Option>
                                    ))}
                                </Dropdown>
                            </Field>
                            <Field label="Agent Assignment">
                                <Dropdown
                                    placeholder="Agent assignment"
                                    value={agentLabel}
                                    onOptionSelect={(_e, data) => setAgentLabel(data.optionText ?? 'Auto-detect')}
                                >
                                            {WORK_ITEM_ASSIGNMENT_OPTION_LABELS.map((label) => (
                                                <Option key={label}>{label}</Option>
                                            ))}
                                        </Dropdown>
                                    </Field>
                        </div>
                    </DialogContent>
                    <DialogActions className={mergeClasses(isMobile && styles.dialogActionsMobile)}>
                        <DialogTrigger disableButtonEnhancement>
                            <Button appearance="secondary" className={mergeClasses(isMobile && styles.dialogActionButtonMobile)}>Cancel</Button>
                        </DialogTrigger>
                        <Button
                            appearance="primary"
                            onClick={handleCreate}
                            disabled={!title.trim() || createMutation.isPending}
                            className={mergeClasses(isMobile && styles.dialogActionButtonMobile)}
                        >
                            {createMutation.isPending ? 'Creating...' : 'Create'}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    )
}
