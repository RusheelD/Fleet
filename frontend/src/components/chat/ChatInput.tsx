import { useRef, useCallback, useEffect } from 'react'
import {
    makeStyles,
    mergeClasses,
    tokens,
    Caption1,
    Button,
    Menu,
    MenuTrigger,
    MenuPopover,
    MenuList,
    MenuItem,
} from '@fluentui/react-components'
import {
    SendRegular,
    AttachRegular,
    ChevronDownRegular,
    TaskListAddRegular,
} from '@fluentui/react-icons'
import { usePreferences } from '../../hooks'

const useStyles = makeStyles({
    inputArea: {
        padding: '0.75rem 1rem',
        borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
        display: 'flex',
        flexDirection: 'column',
        gap: '0.5rem',
        flexShrink: 0,
    },
    inputAreaCompact: {
        paddingTop: '0.375rem',
        paddingBottom: '0.375rem',
        paddingLeft: '0.5rem',
        paddingRight: '0.5rem',
        gap: '0.25rem',
    },
    inputRow: {
        display: 'flex',
        gap: '0.5rem',
        alignItems: 'flex-end',
    },
    inputRowCompact: {
        gap: '0.375rem',
    },
    inputTextarea: {
        flex: 1,
        fontFamily: tokens.fontFamilyBase,
        fontSize: tokens.fontSizeBase300,
        lineHeight: tokens.lineHeightBase300,
        padding: '6px 12px',
        borderRadius: tokens.borderRadiusMedium,
        borderTopWidth: '1px',
        borderRightWidth: '1px',
        borderBottomWidth: '1px',
        borderLeftWidth: '1px',
        borderTopStyle: 'solid',
        borderRightStyle: 'solid',
        borderBottomStyle: 'solid',
        borderLeftStyle: 'solid',
        borderTopColor: tokens.colorNeutralStroke1,
        borderRightColor: tokens.colorNeutralStroke1,
        borderBottomColor: tokens.colorNeutralStroke1,
        borderLeftColor: tokens.colorNeutralStroke1,
        backgroundColor: tokens.colorNeutralBackground1,
        color: tokens.colorNeutralForeground1,
        resize: 'none',
        overflow: 'hidden',
        minHeight: '60px',
        maxHeight: '200px',
        boxSizing: 'border-box',
        ':focus': {
            outlineWidth: '2px',
            outlineStyle: 'solid',
            outlineColor: tokens.colorBrandStroke1,
            borderTopColor: 'transparent',
            borderRightColor: 'transparent',
            borderBottomColor: 'transparent',
            borderLeftColor: 'transparent',
        },
        '::placeholder': {
            color: tokens.colorNeutralForeground4,
        },
    },
    inputTextareaCompact: {
        fontSize: tokens.fontSizeBase200,
        lineHeight: tokens.lineHeightBase200,
        paddingTop: '4px',
        paddingBottom: '4px',
        paddingLeft: '8px',
        paddingRight: '8px',
        minHeight: '44px',
    },
    sendGroup: {
        display: 'flex',
        alignSelf: 'flex-end',
    },
    sendButton: {
        borderTopRightRadius: 0,
        borderBottomRightRadius: 0,
    },
    menuButton: {
        borderTopLeftRadius: 0,
        borderBottomLeftRadius: 0,
        minWidth: 'auto',
        paddingLeft: '4px',
        paddingRight: '4px',
        borderLeft: `1px solid ${tokens.colorNeutralForegroundOnBrand}`,
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
    inputHintCompact: {
        fontSize: '10px',
        lineHeight: '12px',
    },
    hiddenInput: {
        display: 'none',
    },
})

interface ChatInputProps {
    value: string
    onChange: (value: string) => void
    onSend?: () => void
    onGenerate?: () => void
    allowGenerate?: boolean
    onFileSelect?: (file: File) => void
    disabled?: boolean
    uploading?: boolean
}

export function ChatInput({ value, onChange, onSend, onGenerate, allowGenerate = true, onFileSelect, disabled, uploading }: ChatInputProps) {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isCompact = preferences?.compactMode ?? false
    const fileInputRef = useRef<HTMLInputElement>(null)
    const textareaRef = useRef<HTMLTextAreaElement>(null)

    const hasText = value.trim().length > 0
    const canGenerate = allowGenerate && typeof onGenerate === 'function'

    const autoResize = useCallback(() => {
        const el = textareaRef.current
        if (!el) return
        el.style.height = 'auto'
        el.style.height = `${el.scrollHeight}px`
        // Toggle overflow when content exceeds maxHeight
        el.style.overflow = el.scrollHeight > 200 ? 'auto' : 'hidden'
    }, [])

    useEffect(() => {
        autoResize()
    }, [value, autoResize])

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
        <div className={mergeClasses(styles.inputArea, isCompact && styles.inputAreaCompact)}>
            <div className={mergeClasses(styles.inputRow, isCompact && styles.inputRowCompact)}>
                <textarea
                    ref={textareaRef}
                    placeholder="Describe what you want to build..."
                    value={value}
                    onChange={(e) => onChange(e.target.value)}
                    rows={2}
                    className={mergeClasses(styles.inputTextarea, isCompact && styles.inputTextareaCompact)}
                    disabled={disabled}
                    onKeyDown={(e) => {
                        if (e.key === 'Enter' && !e.shiftKey) {
                            e.preventDefault()
                            if (hasText && onSend) onSend()
                        }
                    }}
                />
                <div className={styles.sendGroup}>
                    {canGenerate ? (
                        <>
                            <Button
                                appearance="primary"
                                icon={hasText ? <SendRegular /> : <TaskListAddRegular />}
                                disabled={disabled}
                                className={styles.sendButton}
                                size={isCompact ? 'small' : 'medium'}
                                onClick={hasText ? onSend : onGenerate}
                            >
                                {hasText ? 'Send' : 'Generate'}
                            </Button>
                            <Menu>
                                <MenuTrigger disableButtonEnhancement>
                                    <Button
                                        appearance="primary"
                                        icon={<ChevronDownRegular />}
                                        disabled={disabled}
                                        className={styles.menuButton}
                                        size={isCompact ? 'small' : 'medium'}
                                    />
                                </MenuTrigger>
                                <MenuPopover>
                                    <MenuList>
                                        <MenuItem
                                            icon={<SendRegular />}
                                            disabled={!hasText}
                                            onClick={onSend}
                                        >
                                            Send
                                        </MenuItem>
                                        <MenuItem
                                            icon={<TaskListAddRegular />}
                                            onClick={onGenerate}
                                        >
                                            {hasText ? 'Send & Generate' : 'Generate Work Items'}
                                        </MenuItem>
                                    </MenuList>
                                </MenuPopover>
                            </Menu>
                        </>
                    ) : (
                        <Button
                            appearance="primary"
                            icon={<SendRegular />}
                            disabled={!hasText || disabled}
                            size={isCompact ? 'small' : 'medium'}
                            onClick={onSend}
                        >
                            Send
                        </Button>
                    )}
                </div>
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
                <Caption1 className={mergeClasses(styles.inputHint, isCompact && styles.inputHintCompact)}>
                    Attach .md files for AI context
                </Caption1>
            </div>
        </div>
    )
}
