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
import { AddRegular } from '@fluentui/react-icons'

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
    open: boolean
    onOpenChange: (open: boolean) => void
}

export function CreateWorkItemDialog({ open, onOpenChange }: CreateWorkItemDialogProps) {
    const styles = useStyles()

    return (
        <Dialog open={open} onOpenChange={(_e, data) => onOpenChange(data.open)}>
            <DialogTrigger disableButtonEnhancement>
                <Button appearance="primary" icon={<AddRegular />}>
                    New Work Item
                </Button>
            </DialogTrigger>
            <DialogSurface>
                <DialogBody>
                    <DialogTitle>Create Work Item</DialogTitle>
                    <DialogContent>
                        <div className={styles.dialogForm}>
                            <Field label="Title" required>
                                <Input placeholder="Enter work item title" />
                            </Field>
                            <Field label="Description">
                                <Textarea placeholder="Describe the work item..." resize="vertical" rows={4} />
                            </Field>
                            <div className={styles.dialogFormGrid}>
                                <Field label="Priority">
                                    <Dropdown placeholder="Select priority" defaultValue="P2 — High">
                                        <Option>P1 — Critical</Option>
                                        <Option>P2 — High</Option>
                                        <Option>P3 — Medium</Option>
                                        <Option>P4 — Low</Option>
                                    </Dropdown>
                                </Field>
                                <Field label="State">
                                    <Dropdown placeholder="Select state" defaultValue="New">
                                        <Option>New</Option>
                                        <Option>Active</Option>
                                        <Option>In Progress</Option>
                                        <Option>Resolved</Option>
                                        <Option>Closed</Option>
                                    </Dropdown>
                                </Field>
                            </div>
                            <Field label="Tags">
                                <Input placeholder="e.g. frontend, backend, api" />
                            </Field>
                            <Field label="Agent Assignment">
                                <Dropdown placeholder="Agent assignment" defaultValue="Auto-detect">
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
                        <Button appearance="primary" onClick={() => onOpenChange(false)}>Create</Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    )
}
