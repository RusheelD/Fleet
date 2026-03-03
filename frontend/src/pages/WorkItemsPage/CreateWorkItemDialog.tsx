import { useState } from 'react'
import {
    makeStyles,
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

const PRIORITY_MAP: Record<string, number> = {
    'P1 — Critical': 1,
    'P2 — High': 2,
    'P3 — Medium': 3,
    'P4 — Low': 4,
}

const AGENT_MAP: Record<string, boolean> = {
    'Auto-detect': true,
    '1 agent': true,
    '3 agents': true,
    '5 agents': true,
    'Manual assignment': false,
}

const NONE_PARENT = '(None)'
const NONE_LEVEL = '(None)'

const useStyles = makeStyles({
    dialogForm: {
        display: 'flex',
        flexDirection: 'column',
        gap: '1rem',
    },
    dialogFormGrid: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: '0.75rem',
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
    const createMutation = useCreateWorkItem(projectId)

    const [title, setTitle] = useState('')
    const [description, setDescription] = useState('')
    const [priorityLabel, setPriorityLabel] = useState('P2 — High')
    const [state, setState] = useState('New')
    const [tags, setTags] = useState('')
    const [agentLabel, setAgentLabel] = useState('Auto-detect')
    const [parentLabel, setParentLabel] = useState(NONE_PARENT)
    const [levelLabel, setLevelLabel] = useState(NONE_LEVEL)

    const parentOptions = workItems ?? []
    const sortedLevels = [...(levels ?? [])].sort((a, b) => a.ordinal - b.ordinal)

    const resetForm = () => {
        setTitle('')
        setDescription('')
        setPriorityLabel('P2 — High')
        setState('New')
        setTags('')
        setAgentLabel('Auto-detect')
        setParentLabel(NONE_PARENT)
        setLevelLabel(NONE_LEVEL)
    }

    const handleCreate = () => {
        if (!title.trim()) return
        const selectedParent = parentOptions.find((wi) => `#${wi.id} ${wi.title}` === parentLabel)
        const selectedLevel = sortedLevels.find((l) => l.name === levelLabel)
        createMutation.mutate(
            {
                title: title.trim(),
                description: description.trim(),
                priority: PRIORITY_MAP[priorityLabel] ?? 2,
                state,
                assignedTo: agentLabel === 'Manual assignment' ? 'Unassigned' : 'Fleet AI',
                tags: tags
                    .split(',')
                    .map((t) => t.trim())
                    .filter(Boolean),
                isAI: AGENT_MAP[agentLabel] ?? true,
                parentId: selectedParent?.id ?? null,
                levelId: selectedLevel?.id ?? null,
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
            <DialogSurface>
                <DialogBody>
                    <DialogTitle>Create Work Item</DialogTitle>
                    <DialogContent>
                        <div className={styles.dialogForm}>
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
                            <div className={styles.dialogFormGrid}>
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
                                        onOptionSelect={(_e, data) => setPriorityLabel(data.optionText ?? 'P2 — High')}
                                    >
                                        <Option>P1 — Critical</Option>
                                        <Option>P2 — High</Option>
                                        <Option>P3 — Medium</Option>
                                        <Option>P4 — Low</Option>
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
                                        <Option>Resolved</Option>
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
                                        <Option key={wi.id}>{`#${wi.id} ${wi.title}`}</Option>
                                    ))}
                                </Dropdown>
                            </Field>
                            <Field label="Agent Assignment">
                                <Dropdown
                                    placeholder="Agent assignment"
                                    value={agentLabel}
                                    onOptionSelect={(_e, data) => setAgentLabel(data.optionText ?? 'Auto-detect')}
                                >
                                    <Option>Auto-detect</Option>
                                    <Option>1 agent</Option>
                                    <Option>3 agents</Option>
                                    <Option>5 agents</Option>
                                    <Option>Manual assignment</Option>
                                </Dropdown>
                            </Field>
                        </div>
                    </DialogContent>
                    <DialogActions>
                        <DialogTrigger disableButtonEnhancement>
                            <Button appearance="secondary">Cancel</Button>
                        </DialogTrigger>
                        <Button
                            appearance="primary"
                            onClick={handleCreate}
                            disabled={!title.trim() || createMutation.isPending}
                        >
                            {createMutation.isPending ? 'Creating...' : 'Create'}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    )
}
