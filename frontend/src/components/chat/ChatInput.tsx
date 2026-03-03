import { useRef } from 'react'
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
    hiddenInput: {
        display: 'none',
    },
})

interface ChatInputProps {
    value: string
    onChange: (value: string) => void
    onSend?: () => void
    onFileSelect?: (file: File) => void
    disabled?: boolean
    uploading?: boolean
}

export function ChatInput({ value, onChange, onSend, onFileSelect, disabled, uploading }: ChatInputProps) {
    const styles = useStyles()
    const fileInputRef = useRef<HTMLInputElement>(null)

    const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        const file = e.target.files?.[0]
        if (file && onFileSelect) {
            onFileSelect(file)
        }
        // Reset so the same file can be re-selected
        if (fileInputRef.current) {
            fileInputRef.current.value = ''
        }
    }

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
                    disabled={disabled}
                />
                <Button
                    appearance="primary"
                    icon={<SendRegular />}
                    disabled={!value.trim() || disabled}
                    className={styles.sendButton}
                    onClick={onSend}
                >
                    Send
                </Button>
            </div>
            <div className={styles.inputActions}>
                <div className={styles.inputButtons}>
                    <input
                        ref={fileInputRef}
                        type="file"
                        accept=".md"
                        className={styles.hiddenInput}
                        onChange={handleFileChange}
                    />
                    <Button
                        appearance="subtle"
                        size="small"
                        icon={<AttachRegular />}
                        onClick={() => fileInputRef.current?.click()}
                        disabled={disabled || uploading}
                    >
                        {uploading ? 'Uploading...' : 'Attach .md'}
                    </Button>
                </div>
                <Caption1 className={styles.inputHint}>
                    Attach .md files for AI context
                </Caption1>
            </div>
        </div>
    )
}
