import {
    makeStyles,
    tokens,
    Caption1,
    Button,
    Textarea,
} from '@fluentui/react-components'
import {
    SendRegular,
    AttachRegular,
    DocumentRegular,
    ArrowUploadRegular,
} from '@fluentui/react-icons'

const useStyles = makeStyles({
    inputArea: {
        padding: '0.75rem 1rem',
        borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
        display: 'flex',
        flexDirection: 'column',
        gap: '0.5rem',
    },
    inputRow: {
        display: 'flex',
        gap: '0.5rem',
        alignItems: 'flex-end',
    },
    inputTextarea: {
        flex: 1,
    },
    sendButton: {
        alignSelf: 'flex-end',
    },
    inputActions: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    inputButtons: {
        display: 'flex',
        gap: '0.25rem',
    },
    inputHint: {
        color: tokens.colorNeutralForeground4,
    },
})

interface ChatInputProps {
    value: string
    onChange: (value: string) => void
    onSend?: () => void
}

export function ChatInput({ value, onChange, onSend }: ChatInputProps) {
    const styles = useStyles()

    return (
        <div className={styles.inputArea}>
            <div className={styles.inputRow}>
                <Textarea
                    placeholder="Describe what you want to build..."
                    value={value}
                    onChange={(_e, data) => onChange(data.value)}
                    resize="vertical"
                    rows={2}
                    className={styles.inputTextarea}
                />
                <Button
                    appearance="primary"
                    icon={<SendRegular />}
                    disabled={!value.trim()}
                    className={styles.sendButton}
                    onClick={onSend}
                >
                    Send
                </Button>
            </div>
            <div className={styles.inputActions}>
                <div className={styles.inputButtons}>
                    <Button appearance="subtle" size="small" icon={<AttachRegular />}>
                        Attach
                    </Button>
                    <Button appearance="subtle" size="small" icon={<DocumentRegular />}>
                        Repo
                    </Button>
                    <Button appearance="subtle" size="small" icon={<ArrowUploadRegular />}>
                        Upload
                    </Button>
                </div>
                <Caption1 className={styles.inputHint}>
                    Not streamed in this version
                </Caption1>
            </div>
        </div>
    )
}
