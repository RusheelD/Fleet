import { useState, useEffect, useMemo, useCallback, useRef } from 'react'
import {
    makeStyles,
    mergeClasses,
    Button,
    Input,
    Dropdown,
    Option,
    Textarea,
    Badge,
    Text,
    Caption1,
    Divider,
    Tooltip,
    Link,
} from '@fluentui/react-components'
import {
    SaveRegular,
    DeleteRegular,
    DismissRegular,
    BotRegular,
    PersonRegular,
    ChevronRightRegular,
} from '@fluentui/react-icons'
import {
    useUpdateWorkItem,
    useDeleteWorkItem,
    useWorkItemAttachments,
    useUploadWorkItemAttachment,
    useDeleteWorkItemAttachment,
    getApiErrorMessage,
    resolveLevelIcon,
} from '../../proxies'
import type { WorkItem, WorkItemAttachment, WorkItemLevel } from '../../models'
import { useIsMobile } from '../../hooks'
import { StateDot } from './StateDot'
import { PriorityDot } from './PriorityDot'
import { formatWorkItemState } from './stateLabel'
import { appTokens } from '../../styles/appTokens'
import Markdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { AuthorizedAttachmentImage, AuthorizedAttachmentLink } from '../../components/shared/AuthorizedAttachment'
import {
    AUTO_ASSIGNMENT_LABEL,
    MANUAL_ASSIGNMENT_LABEL,
    WORK_ITEM_ASSIGNMENT_OPTION_LABELS,
    getWorkItemAssignmentLabel,
    getWorkItemAssignmentSettings,
} from './workItemAssignmentOptions'

const NONE_PARENT = '(None)'
const NONE_LEVEL = '(None)'

const PRIORITY_LABELS: Record<number, string> = {
    1: 'P1 - Critical',
    2: 'P2 - High',
    3: 'P3 - Medium',
    4: 'P4 - Low',
}

const PRIORITY_MAP: Record<string, number> = {
    'P1 - Critical': 1,
    'P2 - High': 2,
    'P3 - Medium': 3,
    'P4 - Low': 4,
}

const DIFFICULTY_LABELS: Record<number, string> = {
    1: 'D1 - Very Easy',
    2: 'D2 - Easy',
    3: 'D3 - Medium',
    4: 'D4 - Hard',
    5: 'D5 - Very Hard',
}

const DIFFICULTY_MAP: Record<string, number> = {
    'D1 - Very Easy': 1,
    'D2 - Easy': 2,
    'D3 - Medium': 3,
    'D4 - Hard': 4,
    'D5 - Very Hard': 5,
}

const useStyles = makeStyles({
    /* Overlay backdrop */
    overlay: {
        position: 'fixed',
        inset: 0,
        backgroundColor: appTokens.color.overlayBackdrop,
        zIndex: appTokens.zIndex.workItemOverlay,
    },

    /* Panel container */
    panel: {
        position: 'fixed',
        top: 0,
        right: 0,
        bottom: 0,
        width: 'min(960px, 100vw)',
        maxWidth: '100vw',
        backgroundColor: appTokens.color.surface,
        boxShadow: appTokens.shadow.overlay,
        zIndex: appTokens.zIndex.workItemPanel,
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
    },
    panelMobile: {
        width: '100vw',
        left: 0,
    },

    /* Header bar (type badge + ID + title) */
    headerBar: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.md,
        paddingTop: appTokens.space.md,
        paddingBottom: appTokens.space.md,
        paddingLeft: appTokens.space.xl,
        paddingRight: appTokens.space.md,
        borderBottom: appTokens.border.subtle,
        backgroundColor: appTokens.color.surfaceAlt,
        flexShrink: 0,
    },
    headerBarMobile: {
        flexWrap: 'wrap',
        gap: appTokens.space.sm,
        paddingTop: appTokens.space.sm,
        paddingBottom: appTokens.space.sm,
        paddingLeft: appTokens.space.lg,
        paddingRight: appTokens.space.lg,
    },
    headerType: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.xs,
        flexShrink: 0,
    },
    headerTypeIcon: {
        fontSize: appTokens.fontSize.xl,
        display: 'flex',
        alignItems: 'center',
    },
    headerId: {
        color: appTokens.color.textSecondary,
        fontSize: appTokens.fontSize.sm,
        flexShrink: 0,
    },
    headerTitle: {
        flex: 1,
        minWidth: 0,
    },
    headerTitleMobile: {
        flexBasis: '100%',
        order: 2,
    },
    headerTitleInput: {
        fontWeight: appTokens.fontWeight.semibold,
        fontSize: appTokens.fontSize.lg,
    },
    headerActions: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.xs,
        flexShrink: 0,
    },

    /* Action bar (Save / Delete) */
    actionBar: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        paddingTop: appTokens.space.sm,
        paddingBottom: appTokens.space.sm,
        paddingLeft: appTokens.space.xl,
        paddingRight: appTokens.space.xl,
        borderBottom: appTokens.border.subtleAlt,
        flexShrink: 0,
    },
    actionBarMobile: {
        flexDirection: 'column',
        alignItems: 'stretch',
        gap: appTokens.space.sm,
        paddingTop: appTokens.space.sm,
        paddingBottom: appTokens.space.sm,
        paddingLeft: appTokens.space.lg,
        paddingRight: appTokens.space.lg,
    },
    actionBarLeft: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.sm,
    },
    actionBarLeftMobile: {
        width: '100%',
        display: 'grid',
        gap: appTokens.space.xs,
    },
    actionButtonMobile: {
        width: '100%',
    },
    deleteButton: {
        color: appTokens.color.danger,
    },

    /* Scrollable body */
    body: {
        display: 'flex',
        flexDirection: 'column',
        flex: 1,
        overflow: 'auto',
        paddingTop: appTokens.space.xl,
        paddingBottom: appTokens.space.xxl,
        paddingLeft: appTokens.space.xxl,
        paddingRight: appTokens.space.xxl,
        gap: appTokens.space.xl,
    },
    bodyMobile: {
        paddingTop: appTokens.space.md,
        paddingBottom: appTokens.space.xl,
        paddingLeft: appTokens.space.lg,
        paddingRight: appTokens.space.lg,
        gap: appTokens.space.md,
    },

    /* Fields grid (compact strip across top) */
    fieldsGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(3, 1fr)',
        gap: `${appTokens.space.md} ${appTokens.space.xl}`,
        paddingTop: appTokens.space.sm,
        paddingBottom: appTokens.space.md,
        borderBottom: appTokens.border.subtleAlt,
    },
    fieldsGridMobile: {
        gridTemplateColumns: '1fr',
        gap: `${appTokens.space.sm} ${appTokens.space.sm}`,
    },

    /* Description (full width, fills remaining space) */
    descriptionSection: {
        display: 'flex',
        flexDirection: 'column',
        flex: 1,
        minHeight: '200px',
    },
    descriptionSectionMobile: {
        minHeight: '140px',
    },
    descriptionTextarea: {
        marginTop: '8px',
        flex: 1,
        '& > textarea': {
            minHeight: '200px',
        },
    },
    descriptionTextareaMobile: {
        '& > textarea': {
            minHeight: '140px',
        },
    },
    markdownPreview: {
        marginTop: appTokens.space.sm,
        padding: appTokens.space.md,
        borderRadius: appTokens.radius.md,
        border: appTokens.border.subtle,
        backgroundColor: appTokens.color.surfaceAlt,
        minHeight: '3rem',
        '& p': {
            marginTop: 0,
            marginBottom: appTokens.space.sm,
        },
        '& p:last-child': {
            marginBottom: 0,
        },
        '& ul, & ol': {
            marginTop: 0,
            marginBottom: appTokens.space.sm,
            paddingLeft: '1.25rem',
        },
        '& li': {
            marginBottom: appTokens.space.xxs,
        },
        '& code': {
            fontFamily: appTokens.fontFamily.monospace,
            fontSize: appTokens.fontSize.sm,
            padding: '0.1rem 0.35rem',
            borderRadius: appTokens.radius.sm,
            backgroundColor: appTokens.color.surfaceRaised,
        },
        '& pre': {
            overflowX: 'auto',
            padding: appTokens.space.sm,
            borderRadius: appTokens.radius.md,
            backgroundColor: appTokens.color.surfaceRaised,
        },
        '& img': {
            maxWidth: '100%',
            height: 'auto',
            borderRadius: appTokens.radius.md,
        },
    },
    assetsSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.sm,
        paddingTop: appTokens.space.sm,
        paddingBottom: appTokens.space.md,
        borderBottom: appTokens.border.subtleAlt,
    },
    assetsHeader: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: appTokens.space.sm,
        flexWrap: 'wrap',
    },
    assetsTitleGroup: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxs,
    },
    assetList: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.sm,
    },
    assetCard: {
        display: 'grid',
        gridTemplateColumns: 'minmax(0, 1fr)',
        gap: appTokens.space.sm,
        padding: appTokens.space.md,
        borderRadius: appTokens.radius.md,
        border: appTokens.border.subtle,
        backgroundColor: appTokens.color.surfaceAlt,
    },
    assetPreview: {
        maxWidth: 'min(320px, 100%)',
    },
    assetMeta: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxs,
        minWidth: 0,
    },
    assetFileName: {
        fontWeight: appTokens.fontWeight.semibold,
        overflowWrap: 'anywhere',
    },
    assetActions: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: appTokens.space.xs,
        alignItems: 'center',
    },
    assetLink: {
        color: appTokens.color.brand,
    },
    hiddenInput: {
        display: 'none',
    },
    emptyAssets: {
        color: appTokens.color.textMuted,
    },
    assetError: {
        color: appTokens.color.danger,
    },

    /* Section headers */
    sectionTitle: {
        fontWeight: appTokens.fontWeight.semibold,
        fontSize: appTokens.fontSize.md,
        color: appTokens.color.textSecondary,
        textTransform: 'uppercase',
        letterSpacing: '0.03em',
    },

    /* Field rows */
    fieldRow: {
        display: 'flex',
        flexDirection: 'column',
        gap: '2px',
    },
    fieldLabel: {
        fontSize: appTokens.fontSize.xs,
        fontWeight: appTokens.fontWeight.semibold,
        color: appTokens.color.textTertiary,
        textTransform: 'uppercase',
        letterSpacing: '0.04em',
    },
    fieldValue: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.xs,
        fontSize: appTokens.fontSize.md,
        minHeight: '28px',
    },
    fieldValueMobile: {
        flexWrap: 'wrap',
        alignItems: 'flex-start',
    },
    fieldDropdown: {
        minWidth: 0,
        width: '100%',
    },

    /* Children section */
    childRow: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.xs,
        padding: '4px 8px',
        borderRadius: appTokens.radius.md,
        cursor: 'default',
        fontSize: appTokens.fontSize.md,
        ':hover': {
            backgroundColor: appTokens.color.surfaceHover,
        },
    },
    childRowMobile: {
        flexWrap: 'wrap',
        rowGap: '2px',
    },
    childIcon: {
        fontSize: '14px',
        display: 'flex',
        alignItems: 'center',
        flexShrink: 0,
    },
    childTitle: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        flex: 1,
    },
    childTitleMobile: {
        whiteSpace: 'normal',
        overflow: 'visible',
        textOverflow: 'clip',
        flexBasis: '100%',
    },
    childId: {
        color: appTokens.color.textTertiary,
        fontSize: appTokens.fontSize.sm,
        flexShrink: 0,
    },

    /* AI badge */
    aiBadge: {
        flexShrink: 0,
    },
})

interface WorkItemDetailDialogProps {
    projectId: string
    item: WorkItem | null
    workItems?: WorkItem[]
    levels?: WorkItemLevel[]
    onClose: () => void
    onNavigate?: (item: WorkItem) => void
}

export function WorkItemDetailDialog({ projectId, item, workItems, levels, onClose, onNavigate }: WorkItemDetailDialogProps) {
    const styles = useStyles()
    const isMobile = useIsMobile()
    const updateMutation = useUpdateWorkItem(projectId)
    const deleteMutation = useDeleteWorkItem(projectId)
    const attachmentsQuery = useWorkItemAttachments(projectId, item?.workItemNumber)
    const uploadAttachmentMutation = useUploadWorkItemAttachment(projectId)
    const deleteAttachmentMutation = useDeleteWorkItemAttachment(projectId, item?.workItemNumber)
    const fileInputRef = useRef<HTMLInputElement | null>(null)

    const [title, setTitle] = useState('')
    const [description, setDescription] = useState('')
    const [acceptanceCriteria, setAcceptanceCriteria] = useState('')
    const [priorityLabel, setPriorityLabel] = useState('P2 - High')
    const [difficultyLabel, setDifficultyLabel] = useState('D3 - Medium')
    const [state, setState] = useState('New')
    const [tags, setTags] = useState('')
    const [assignedTo, setAssignedTo] = useState('')
    const [agentLabel, setAgentLabel] = useState(AUTO_ASSIGNMENT_LABEL)
    const [parentLabel, setParentLabel] = useState(NONE_PARENT)
    const [levelLabel, setLevelLabel] = useState(NONE_LEVEL)
    const attachments = attachmentsQuery.data ?? []
    const attachmentError = attachmentsQuery.error
        ?? uploadAttachmentMutation.error
        ?? deleteAttachmentMutation.error

    const sortedLevels = useMemo(() => [...(levels ?? [])].sort((a, b) => a.ordinal - b.ordinal), [levels])

    const parentOptions = useMemo(() => {
        if (!item || !workItems) return []
        const descendantIds = new Set<number>()
        const collectDescendants = (witNum: number) => {
            for (const wi of workItems) {
                if (wi.parentWorkItemNumber === witNum && !descendantIds.has(wi.workItemNumber)) {
                    descendantIds.add(wi.workItemNumber)
                    collectDescendants(wi.workItemNumber)
                }
            }
        }
        descendantIds.add(item.workItemNumber)
        collectDescendants(item.workItemNumber)
        return workItems.filter((wi) => !descendantIds.has(wi.workItemNumber))
    }, [item, workItems])

    const children = useMemo(() => {
        if (!item || !workItems) return []
        return workItems.filter((wi) => wi.parentWorkItemNumber === item.workItemNumber)
    }, [item, workItems])

    const currentLevel = useMemo(() => {
        const selected = sortedLevels.find((l) => l.name === levelLabel)
        if (selected) return selected
        if (item?.levelId != null) return levels?.find((l) => l.id === item.levelId)
        return undefined
    }, [levelLabel, sortedLevels, item, levels])

    useEffect(() => {
        if (item) {
            setTitle(item.title)
            setDescription(item.description)
            setAcceptanceCriteria(item.acceptanceCriteria ?? '')
            setPriorityLabel(PRIORITY_LABELS[item.priority] ?? 'P2 - High')
            setDifficultyLabel(DIFFICULTY_LABELS[item.difficulty] ?? 'D3 - Medium')
            setState(item.state)
            setTags(item.tags.join(', '))
            setAssignedTo(item.assignedTo)
            setAgentLabel(getWorkItemAssignmentLabel(item))
            const parent = workItems?.find((wi) => wi.workItemNumber === item.parentWorkItemNumber)
            setParentLabel(parent ? `#${parent.workItemNumber} ${parent.title}` : NONE_PARENT)
            const lvl = sortedLevels.find((l) => l.id === item.levelId)
            setLevelLabel(lvl ? lvl.name : NONE_LEVEL)
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps -- reset form only when item changes, not on every levels refetch
    }, [item?.workItemNumber])

    /* Escape key handler */
    const handleKeyDown = useCallback((e: KeyboardEvent) => {
        if (e.key === 'Escape') onClose()
    }, [onClose])

    useEffect(() => {
        if (item) {
            document.addEventListener('keydown', handleKeyDown)
            return () => document.removeEventListener('keydown', handleKeyDown)
        }
    }, [item, handleKeyDown])

    const handleSave = () => {
        if (!item || !title.trim()) return
        const selectedParent = parentOptions.find((wi) => `#${wi.workItemNumber} ${wi.title}` === parentLabel)
        const selectedLevel = sortedLevels.find((l) => l.name === levelLabel)
        const agentSettings = getWorkItemAssignmentSettings(agentLabel, assignedTo)
        updateMutation.mutate(
            {
                workItemNumber: item.workItemNumber,
                data: {
                    title: title.trim(),
                    description: description.trim(),
                    priority: PRIORITY_MAP[priorityLabel] ?? 2,
                    difficulty: DIFFICULTY_MAP[difficultyLabel] ?? 3,
                    state,
                    assignedTo: agentSettings.assignedTo,
                    isAI: agentSettings.isAI,
                    assignmentMode: agentSettings.assignmentMode,
                    assignedAgentCount: agentSettings.assignedAgentCount,
                    acceptanceCriteria: acceptanceCriteria.trim(),
                    tags: tags
                        .split(',')
                        .map((t) => t.trim())
                        .filter(Boolean),
                    parentWorkItemNumber: parentLabel === NONE_PARENT ? 0 : (selectedParent?.workItemNumber ?? item.parentWorkItemNumber),
                    levelId: levelLabel === NONE_LEVEL ? 0 : (selectedLevel?.id ?? item.levelId),
                },
            },
            { onSuccess: () => onClose() },
        )
    }

    const handleDelete = () => {
        if (!item) return
        deleteMutation.mutate(item.workItemNumber, { onSuccess: () => onClose() })
    }

    const appendMarkdownReference = useCallback((currentValue: string, attachment: WorkItemAttachment) => {
        const trimmedValue = currentValue.trimEnd()
        if (!trimmedValue) {
            return attachment.markdownReference
        }

        if (trimmedValue.includes(attachment.markdownReference)) {
            return currentValue
        }

        return `${trimmedValue}\n\n${attachment.markdownReference}`
    }, [])

    const formatAttachmentSize = useCallback((contentLength: number) => {
        if (contentLength < 1024) {
            return `${contentLength} B`
        }

        if (contentLength < 1024 * 1024) {
            return `${(contentLength / 1024).toFixed(1)} KB`
        }

        return `${(contentLength / (1024 * 1024)).toFixed(1)} MB`
    }, [])

    const handleUploadClick = useCallback(() => {
        fileInputRef.current?.click()
    }, [])

    const handleAssetInputChange = useCallback(async (event: React.ChangeEvent<HTMLInputElement>) => {
        if (!item) {
            return
        }

        const files = Array.from(event.target.files ?? [])
        if (files.length === 0) {
            return
        }

        for (const file of files) {
            await uploadAttachmentMutation.mutateAsync({
                workItemNumber: item.workItemNumber,
                file,
            })
        }

        event.target.value = ''
    }, [item, uploadAttachmentMutation])

    if (!item) return null

    return (
        <>
            {/* Backdrop */}
            <div className={styles.overlay} onClick={onClose} />

            {/* Panel */}
            <div className={mergeClasses(styles.panel, isMobile && styles.panelMobile)}>
                {/* Header bar */}
                <div className={mergeClasses(styles.headerBar, isMobile && styles.headerBarMobile)}>
                    <div className={styles.headerType}>
                        {currentLevel ? (
                            <>
                                <span className={styles.headerTypeIcon} style={{ color: currentLevel.color }}>
                                    {resolveLevelIcon(currentLevel.iconName)}
                                </span>
                                <Text weight="semibold" style={{ color: currentLevel.color }}>
                                    {currentLevel.name}
                                </Text>
                            </>
                        ) : (
                            <Text weight="semibold" style={{ color: appTokens.color.textTertiary }}>
                                Work Item
                            </Text>
                        )}
                    </div>
                    <Text className={styles.headerId}>{item.workItemNumber}</Text>
                    {item.isAI && (
                        <Badge appearance="filled" color="brand" size="small" className={styles.aiBadge}>
                            AI
                        </Badge>
                    )}
                    <div className={mergeClasses(styles.headerTitle, isMobile && styles.headerTitleMobile)}>
                        <Input
                            className={styles.headerTitleInput}
                            appearance="underline"
                            value={title}
                            onChange={(_e, data) => setTitle(data.value)}
                        />
                    </div>
                    <div className={styles.headerActions}>
                        <Tooltip content="Close" relationship="label">
                            <Button
                                appearance="subtle"
                                icon={<DismissRegular />}
                                onClick={onClose}
                                aria-label="Close"
                            />
                        </Tooltip>
                    </div>
                </div>

                {/* Action bar */}
                <div className={mergeClasses(styles.actionBar, isMobile && styles.actionBarMobile)}>
                    <div className={mergeClasses(styles.actionBarLeft, isMobile && styles.actionBarLeftMobile)}>
                        <Button
                            appearance="primary"
                            icon={<SaveRegular />}
                            size="small"
                            onClick={handleSave}
                            disabled={!title.trim() || updateMutation.isPending}
                            className={mergeClasses(isMobile && styles.actionButtonMobile)}
                        >
                            {updateMutation.isPending ? 'Saving...' : 'Save'}
                        </Button>
                        <Button appearance="subtle" size="small" onClick={onClose} className={mergeClasses(isMobile && styles.actionButtonMobile)}>
                            Cancel
                        </Button>
                    </div>
                    <Button
                        appearance="subtle"
                        icon={<DeleteRegular />}
                        size="small"
                        className={styles.deleteButton}
                        onClick={handleDelete}
                        disabled={deleteMutation.isPending}
                        style={isMobile ? { width: '100%' } : undefined}
                    >
                        Delete
                    </Button>
                </div>

                {/* Body: fields grid -> description -> children */}
                <div className={mergeClasses(styles.body, isMobile && styles.bodyMobile)}>
                    {/* Detail fields in a compact grid */}
                    <div className={mergeClasses(styles.fieldsGrid, isMobile && styles.fieldsGridMobile)}>
                        {/* State */}
                        <div className={styles.fieldRow}>
                            <Text className={styles.fieldLabel}>State</Text>
                            <Dropdown
                                className={styles.fieldDropdown}
                                size="small"
                                value={state}
                                onOptionSelect={(_e, data) => setState(data.optionText ?? 'New')}
                            >
                                <Option>New</Option>
                                <Option>Active</Option>
                                <Option>Planning (AI)</Option>
                                <Option>In Progress</Option>
                                <Option>In Progress (AI)</Option>
                                <Option>In-PR</Option>
                                <Option>In-PR (AI)</Option>
                                <Option>Resolved</Option>
                                <Option>Resolved (AI)</Option>
                                <Option>Closed</Option>
                            </Dropdown>
                        </div>

                        {/* Level */}
                        <div className={styles.fieldRow}>
                            <Text className={styles.fieldLabel}>Work Item Type</Text>
                            <Dropdown
                                className={styles.fieldDropdown}
                                size="small"
                                value={levelLabel}
                                onOptionSelect={(_e, data) => setLevelLabel(data.optionText ?? NONE_LEVEL)}
                            >
                                <Option>{NONE_LEVEL}</Option>
                                {sortedLevels.map((l) => (
                                    <Option key={l.id} text={l.name}>
                                        <span style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}>
                                            <span style={{ color: l.color, display: 'flex', alignItems: 'center' }}>
                                                {resolveLevelIcon(l.iconName)}
                                            </span>
                                            {l.name}
                                        </span>
                                    </Option>
                                ))}
                            </Dropdown>
                        </div>

                        {/* Priority */}
                        <div className={styles.fieldRow}>
                            <Text className={styles.fieldLabel}>Priority</Text>
                            <div className={styles.fieldValue}>
                                <PriorityDot priority={PRIORITY_MAP[priorityLabel] ?? 2} />
                                <Dropdown
                                    className={styles.fieldDropdown}
                                    size="small"
                                    value={priorityLabel}
                                    onOptionSelect={(_e, data) => setPriorityLabel(data.optionText ?? 'P2 - High')}
                                >
                                    <Option>P1 - Critical</Option>
                                    <Option>P2 - High</Option>
                                    <Option>P3 - Medium</Option>
                                    <Option>P4 - Low</Option>
                                </Dropdown>
                            </div>
                        </div>

                        {/* Difficulty */}
                        <div className={styles.fieldRow}>
                            <Text className={styles.fieldLabel}>Difficulty</Text>
                            <Dropdown
                                className={styles.fieldDropdown}
                                size="small"
                                value={difficultyLabel}
                                onOptionSelect={(_e, data) => setDifficultyLabel(data.optionText ?? 'D3 - Medium')}
                            >
                                <Option>D1 - Very Easy</Option>
                                <Option>D2 - Easy</Option>
                                <Option>D3 - Medium</Option>
                                <Option>D4 - Hard</Option>
                                <Option>D5 - Very Hard</Option>
                            </Dropdown>
                        </div>

                        {/* Agent Assignment */}
                        <div className={styles.fieldRow}>
                            <Text className={styles.fieldLabel}>Agent Assignment</Text>
                            <div className={mergeClasses(styles.fieldValue, isMobile && styles.fieldValueMobile)}>
                                {agentLabel !== MANUAL_ASSIGNMENT_LABEL ? <BotRegular /> : <PersonRegular />}
                                <Dropdown
                                    className={styles.fieldDropdown}
                                    size="small"
                                    value={agentLabel}
                                    onOptionSelect={(_e, data) => {
                                        const label = data.optionText ?? AUTO_ASSIGNMENT_LABEL
                                        setAgentLabel(label)
                                        if (label !== MANUAL_ASSIGNMENT_LABEL) {
                                            setAssignedTo('Fleet AI')
                                        }
                                    }}
                                >
                                    {WORK_ITEM_ASSIGNMENT_OPTION_LABELS.map((label) => (
                                        <Option key={label}>{label}</Option>
                                    ))}
                                </Dropdown>
                            </div>
                        </div>

                        {/* Assigned To - only editable for manual assignment */}
                        <div className={styles.fieldRow}>
                            <Text className={styles.fieldLabel}>Assigned To</Text>
                            {agentLabel === MANUAL_ASSIGNMENT_LABEL ? (
                                <Input
                                    size="small"
                                    appearance="underline"
                                    value={assignedTo}
                                    onChange={(_e, data) => setAssignedTo(data.value)}
                                    placeholder="Enter name..."
                                />
                            ) : (
                                <div className={mergeClasses(styles.fieldValue, isMobile && styles.fieldValueMobile)}>
                                    <BotRegular />
                                    <Text size={200}>{assignedTo}</Text>
                                </div>
                            )}
                        </div>

                        <div className={styles.fieldRow}>
                            <Text className={styles.fieldLabel}>Linked PR</Text>
                            {item.linkedPullRequestUrl ? (
                                <Link href={item.linkedPullRequestUrl} target="_blank" rel="noreferrer">
                                    Open pull request
                                </Link>
                            ) : (
                                <Text size={200}>-</Text>
                            )}
                        </div>

                        {/* Parent */}
                        <div className={styles.fieldRow}>
                            <Text className={styles.fieldLabel}>Parent</Text>
                            <Dropdown
                                className={styles.fieldDropdown}
                                size="small"
                                placeholder="Select parent"
                                value={parentLabel}
                                onOptionSelect={(_e, data) => setParentLabel(data.optionText ?? NONE_PARENT)}
                            >
                                <Option>{NONE_PARENT}</Option>
                                {parentOptions.map((wi) => (
                                    <Option key={wi.workItemNumber}>{`#${wi.workItemNumber} ${wi.title}`}</Option>
                                ))}
                            </Dropdown>
                        </div>

                        {/* Tags */}
                        <div className={styles.fieldRow}>
                            <Text className={styles.fieldLabel}>Tags</Text>
                            <Input
                                size="small"
                                appearance="underline"
                                placeholder="tag1, tag2, ..."
                                value={tags}
                                onChange={(_e, data) => setTags(data.value)}
                            />
                        </div>
                    </div>

                    <div className={styles.assetsSection}>
                        <div className={styles.assetsHeader}>
                            <div className={styles.assetsTitleGroup}>
                                <Text className={styles.sectionTitle}>Assets</Text>
                                <Caption1>
                                    Upload images, documents, or other files here, then reference them in the description or acceptance criteria so Fleet builders can use them during runs.
                                </Caption1>
                            </div>
                            <Button
                                appearance="secondary"
                                onClick={handleUploadClick}
                                disabled={uploadAttachmentMutation.isPending}
                            >
                                {uploadAttachmentMutation.isPending ? 'Uploading...' : 'Upload assets'}
                            </Button>
                            <input
                                ref={fileInputRef}
                                className={styles.hiddenInput}
                                type="file"
                                multiple
                                onChange={(event) => {
                                    void handleAssetInputChange(event)
                                }}
                            />
                        </div>
                        {attachmentError ? (
                            <Caption1 className={styles.assetError}>
                                {getApiErrorMessage(attachmentError)}
                            </Caption1>
                        ) : null}
                        {attachmentsQuery.isLoading ? (
                            <Caption1 className={styles.emptyAssets}>
                                Loading assets...
                            </Caption1>
                        ) : null}

                        {!attachmentsQuery.isLoading && attachments.length === 0 ? (
                            <Caption1 className={styles.emptyAssets}>
                                No assets uploaded to this work item yet.
                            </Caption1>
                        ) : (
                            <div className={styles.assetList}>
                                {attachments.map((attachment) => (
                                    <div key={attachment.id} className={styles.assetCard}>
                                        {attachment.isImage && attachment.contentUrl ? (
                                            <AuthorizedAttachmentImage
                                                src={attachment.contentUrl}
                                                alt={attachment.fileName}
                                                className={styles.assetPreview}
                                            />
                                        ) : null}
                                        <div className={styles.assetMeta}>
                                            <Text className={styles.assetFileName}>{attachment.fileName}</Text>
                                            <Caption1>
                                                {formatAttachmentSize(attachment.contentLength)} · {attachment.contentType || 'application/octet-stream'}
                                            </Caption1>
                                        </div>
                                        <div className={styles.assetActions}>
                                            <Button
                                                size="small"
                                                appearance="secondary"
                                                onClick={() => setDescription((current) => appendMarkdownReference(current, attachment))}
                                            >
                                                Insert in Description
                                            </Button>
                                            <Button
                                                size="small"
                                                appearance="secondary"
                                                onClick={() => setAcceptanceCriteria((current) => appendMarkdownReference(current, attachment))}
                                            >
                                                Insert in Criteria
                                            </Button>
                                            <AuthorizedAttachmentLink
                                                className={styles.assetLink}
                                                href={attachment.contentUrl}
                                                downloadName={attachment.fileName}
                                                mode="open"
                                            >
                                                Open
                                            </AuthorizedAttachmentLink>
                                            <Button
                                                size="small"
                                                appearance="subtle"
                                                icon={<DeleteRegular />}
                                                disabled={deleteAttachmentMutation.isPending}
                                                onClick={() => deleteAttachmentMutation.mutate(attachment.id)}
                                            >
                                                Delete
                                            </Button>
                                        </div>
                                    </div>
                                ))}
                            </div>
                        )}
                    </div>

                    {/* Full-width description */}
                    <div className={mergeClasses(styles.descriptionSection, isMobile && styles.descriptionSectionMobile)}>
                        <Text className={styles.sectionTitle}>Acceptance Criteria</Text>
                        <Textarea
                            value={acceptanceCriteria}
                            onChange={(_e, data) => setAcceptanceCriteria(data.value)}
                            resize="vertical"
                            rows={4}
                            placeholder="Define what must be true for this work item to be done. Markdown and uploaded asset images are supported."
                        />
                        {acceptanceCriteria.trim().length > 0 && (
                            <div className={styles.markdownPreview}>
                                <Markdown
                                    remarkPlugins={[remarkGfm]}
                                    components={{
                                        a: ({ children, href }) => (
                                            <AuthorizedAttachmentLink href={href}>{children}</AuthorizedAttachmentLink>
                                        ),
                                        img: ({ src, alt }) => (
                                            <AuthorizedAttachmentImage src={src} alt={alt ?? ''} />
                                        ),
                                    }}
                                >
                                    {acceptanceCriteria}
                                </Markdown>
                            </div>
                        )}
                        <Text className={styles.sectionTitle}>Description</Text>
                        <Textarea
                            className={mergeClasses(styles.descriptionTextarea, isMobile && styles.descriptionTextareaMobile)}
                            value={description}
                            onChange={(_e, data) => setDescription(data.value)}
                            resize="vertical"
                            placeholder="Add a description. Markdown and uploaded asset images are supported."
                        />
                        {description.trim().length > 0 && (
                            <div className={styles.markdownPreview}>
                                <Markdown
                                    remarkPlugins={[remarkGfm]}
                                    components={{
                                        a: ({ children, href }) => (
                                            <AuthorizedAttachmentLink href={href}>{children}</AuthorizedAttachmentLink>
                                        ),
                                        img: ({ src, alt }) => (
                                            <AuthorizedAttachmentImage src={src} alt={alt ?? ''} />
                                        ),
                                    }}
                                >
                                    {description}
                                </Markdown>
                            </div>
                        )}
                    </div>

                    {/* Children */}
                    {children.length > 0 && (
                        <div>
                            <Divider />
                            <Text className={styles.sectionTitle} style={{ marginTop: '12px', marginBottom: '8px', display: 'block' }}>
                                Child Items ({children.length})
                            </Text>
                            {children.map((child) => {
                                const childLevel = child.levelId != null ? levels?.find((l) => l.id === child.levelId) : undefined
                                return (
                                    <div
                                        key={child.workItemNumber}
                                        className={mergeClasses(styles.childRow, isMobile && styles.childRowMobile)}
                                        onClick={() => onNavigate?.(child)}
                                        style={{ cursor: onNavigate ? 'pointer' : 'default' }}
                                    >
                                        {childLevel && (
                                            <span className={styles.childIcon} style={{ color: childLevel.color }}>
                                                {resolveLevelIcon(childLevel.iconName)}
                                            </span>
                                        )}
                                        <Text className={styles.childId}>{child.workItemNumber}</Text>
                                        <ChevronRightRegular fontSize={10} />
                                        <Text className={mergeClasses(styles.childTitle, isMobile && styles.childTitleMobile)}>{child.title}</Text>
                                        <StateDot state={child.state} />
                                        <Caption1>{formatWorkItemState(child.state)}</Caption1>
                                    </div>
                                )
                            })}
                        </div>
                    )}
                </div>
            </div>
        </>
    )
}

