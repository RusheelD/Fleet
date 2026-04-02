import { useRef, useCallback, useEffect } from 'react'
import {
    makeStyles,
    mergeClasses,
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
    DismissCircleRegular,
    InfoRegular,
    WarningRegular,
} from '@fluentui/react-icons'
import { usePreferences, useIsMobile } from '../../hooks'
import { appTokens } from '../../styles/appTokens'

const useStyles = makeStyles({
    inputArea: {
        padding: '0.75rem 1rem',
        borderTop: appTokens.border.subtle,
        backgroundColor: appTokens.color.surfaceAlt,
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.sm,
        flexShrink: 0,
    },
    inputAreaCompact: {
        paddingTop: '0.375rem',
        paddingBottom: '0.375rem',
        paddingLeft: '0.5rem',
        paddingRight: '0.5rem',
        gap: '0.25rem',
    },
    inputAreaMobile: {
        paddingBottom: 'calc(0.75rem + env(safe-area-inset-bottom))',
    },
    inputRow: {
        display: 'flex',
        gap: '0.5rem',
        alignItems: 'flex-end',
        minWidth: 0,
        width: '100%',
    },
    inputRowCompact: {
        gap: '0.375rem',
    },
    inputRowMobile: {
        flexDirection: 'column',
        alignItems: 'stretch',
    },
    inputTextarea: {
        flex: 1,
        minWidth: 0,
        width: '100%',
        fontFamily: 'inherit',
        fontSize: '13px',
        lineHeight: '18px',
        padding: '6px 12px',
        borderRadius: appTokens.radius.md,
        borderTopWidth: '1px',
        borderRightWidth: '1px',
        borderBottomWidth: '1px',
        borderLeftWidth: '1px',
        borderTopStyle: 'solid',
        borderRightStyle: 'solid',
        borderBottomStyle: 'solid',
        borderLeftStyle: 'solid',
        borderTopColor: appTokens.color.border,
        borderRightColor: appTokens.color.border,
        borderBottomColor: appTokens.color.border,
        borderLeftColor: appTokens.color.border,
        backgroundColor: appTokens.color.surface,
        color: appTokens.color.textPrimary,
        resize: 'none',
        overflow: 'hidden',
        minHeight: '60px',
        maxHeight: '200px',
        boxSizing: 'border-box',
        ':focus': {
            outlineWidth: '2px',
            outlineStyle: 'solid',
            outlineColor: appTokens.color.brandStroke,
            borderTopColor: 'transparent',
            borderRightColor: 'transparent',
            borderBottomColor: 'transparent',
            borderLeftColor: 'transparent',
        },
        '::placeholder': {
            color: appTokens.color.textMuted,
        },
    },
    inputTextareaCompact: {
        fontSize: '12px',
        lineHeight: '16px',
        paddingTop: '4px',
        paddingBottom: '4px',
        paddingLeft: '8px',
        paddingRight: '8px',
        minHeight: '44px',
    },
    sendGroup: {
        display: 'flex',
        alignSelf: 'flex-end',
        flexShrink: 0,
    },
    sendGroupMobile: {
        width: '100%',
        alignSelf: 'stretch',
    },
    sendButton: {
        borderTopRightRadius: 0,
        borderBottomRightRadius: 0,
    },
    sendButtonFlexible: {
        flex: 1,
    },
    menuButton: {
        borderTopLeftRadius: 0,
        borderBottomLeftRadius: 0,
        minWidth: 'auto',
        paddingLeft: '4px',
        paddingRight: '4px',
        borderLeft: `1px solid ${appTokens.color.textOnBrand}`,
    },
    inputActions: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: '0.5rem',
        minWidth: 0,
    },
    inputActionsMobile: {
        flexDirection: 'column',
        alignItems: 'stretch',
    },
    inputButtons: {
        display: 'flex',
        gap: '0.25rem',
        flexWrap: 'wrap',
    },
    inputButtonsMobile: {
        width: '100%',
        '> .fui-Button': {
            width: '100%',
        },
    },
    inputButtonsCluster: {
        display: 'flex',
        gap: appTokens.space.xs,
        justifyContent: 'flex-end',
        flexWrap: 'wrap',
        minWidth: 0,
    },
    inputButtonsClusterMobile: {
        width: '100%',
        justifyContent: 'stretch',
        '> *': {
            flex: 1,
        },
    },
    cancelButton: {
        flexShrink: 0,
    },
    inputHint: {
        color: appTokens.color.textMuted,
    },
    inputHintCompact: {
        fontSize: '10px',
        lineHeight: '12px',
    },
    inputHintMobile: {
        alignSelf: 'flex-start',
    },
    hiddenInput: {
        display: 'none',
    },
    statusRow: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.xs,
        minWidth: 0,
    },
    statusText: {
        color: appTokens.color.textSecondary,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    statusTextCompact: {
        fontSize: appTokens.fontSize.xs,
    },
    statusIcon: {
        flexShrink: 0,
        color: appTokens.color.info,
    },
    statusIconBrand: {
        color: appTokens.color.brand,
    },
    statusIconWarning: {
        color: appTokens.color.warning,
    },
    statusIconDanger: {
        color: appTokens.color.danger,
    },
})

interface ChatInputProps {
    value: string
    onChange: (value: string) => void
    onSend?: () => void
  onGenerate?: () => void
  onCancelGeneration?: () => void
  allowGenerate?: boolean
  onFileSelect?: (file: File) => void
  disabled?: boolean
  uploading?: boolean
  forceStackedLayout?: boolean
  isGenerating?: boolean
  canceling?: boolean
  statusMessage?: string | null
  statusState?: 'idle' | 'running' | 'canceling' | 'completed' | 'failed' | 'canceled' | 'interrupted'
}

export function ChatInput({
    value,
    onChange,
    onSend,
    onGenerate,
    onCancelGeneration,
    allowGenerate = true,
    onFileSelect,
    disabled,
    uploading,
    forceStackedLayout = false,
    isGenerating = false,
    canceling = false,
    statusMessage,
    statusState = 'idle',
}: ChatInputProps) {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isMobile = useIsMobile()
    const isCompact = preferences?.compactMode ?? false
    const shouldStackLayout = isMobile || forceStackedLayout
    const fileInputRef = useRef<HTMLInputElement>(null)
    const textareaRef = useRef<HTMLTextAreaElement>(null)

    const hasText = value.trim().length > 0
    const canGenerate = allowGenerate && typeof onGenerate === 'function'
    const showCancelButton = isGenerating || canceling
    const showStatus = Boolean(statusMessage)

    const statusIconClassName = (() => {
        switch (statusState) {
            case 'running':
                return styles.statusIconBrand
            case 'canceling':
            case 'canceled':
            case 'interrupted':
                return styles.statusIconWarning
            case 'failed':
                return styles.statusIconDanger
            default:
                return undefined
        }
    })()

    const statusIcon = (() => {
        switch (statusState) {
            case 'failed':
                return <DismissCircleRegular className={mergeClasses(styles.statusIcon, statusIconClassName)} />
            case 'canceling':
            case 'canceled':
            case 'interrupted':
                return <WarningRegular className={mergeClasses(styles.statusIcon, statusIconClassName)} />
            default:
                return <InfoRegular className={mergeClasses(styles.statusIcon, statusIconClassName)} />
        }
    })()

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
        <div className={mergeClasses(styles.inputArea, isCompact && styles.inputAreaCompact, isMobile && styles.inputAreaMobile)}>
            {showStatus && (
                <div className={styles.statusRow}>
                    {statusIcon}
                    <Caption1 className={mergeClasses(styles.statusText, isCompact && styles.statusTextCompact)}>
                        {statusMessage}
                    </Caption1>
                </div>
            )}
            <div className={mergeClasses(styles.inputRow, isCompact && styles.inputRowCompact, shouldStackLayout && styles.inputRowMobile)}>
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
                <div className={mergeClasses(styles.sendGroup, shouldStackLayout && styles.sendGroupMobile)}>
                    <div className={mergeClasses(styles.inputButtonsCluster, shouldStackLayout && styles.inputButtonsClusterMobile)}>
                        {canGenerate ? (
                            <>
                                <div className={mergeClasses(styles.sendGroup, shouldStackLayout && styles.sendGroupMobile)}>
                                    <Button
                                        appearance="primary"
                                        icon={isGenerating ? <TaskListAddRegular /> : hasText ? <SendRegular /> : <TaskListAddRegular />}
                                        disabled={disabled}
                                        className={mergeClasses(styles.sendButton, shouldStackLayout && styles.sendButtonFlexible)}
                                        size={isCompact ? 'small' : 'medium'}
                                        onClick={hasText ? onSend : onGenerate}
                                    >
                                        {isGenerating ? 'Generating...' : hasText ? 'Send' : 'Generate'}
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
                                </div>
                                {showCancelButton && onCancelGeneration && (
                                    <Button
                                        appearance="outline"
                                        icon={<DismissCircleRegular />}
                                        disabled={canceling}
                                        className={styles.cancelButton}
                                        size={isCompact ? 'small' : 'medium'}
                                        onClick={onCancelGeneration}
                                    >
                                        {canceling ? 'Canceling...' : 'Cancel'}
                                    </Button>
                                )}
                            </>
                        ) : (
                            <>
                                <Button
                                    appearance="primary"
                                    icon={<SendRegular />}
                                    disabled={!hasText || disabled}
                                    size={isCompact ? 'small' : 'medium'}
                                    onClick={onSend}
                                >
                                    Send
                                </Button>
                                {showCancelButton && onCancelGeneration && (
                                    <Button
                                        appearance="outline"
                                        icon={<DismissCircleRegular />}
                                        disabled={canceling}
                                        className={styles.cancelButton}
                                        size={isCompact ? 'small' : 'medium'}
                                        onClick={onCancelGeneration}
                                    >
                                        {canceling ? 'Canceling...' : 'Cancel'}
                                    </Button>
                                )}
                            </>
                        )}
                    </div>
                </div>
            </div>
            <div className={mergeClasses(styles.inputActions, shouldStackLayout && styles.inputActionsMobile)}>
                <div className={mergeClasses(styles.inputButtons, shouldStackLayout && styles.inputButtonsMobile)}>
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
                <Caption1 className={mergeClasses(styles.inputHint, isCompact && styles.inputHintCompact, shouldStackLayout && styles.inputHintMobile)}>
                    Attach .md files for AI context
                </Caption1>
            </div>
        </div>
    )
}
