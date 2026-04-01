import {
    makeStyles,
    mergeClasses,
    Badge,
    Button,
    Text,
    Tooltip,
} from '@fluentui/react-components'
import {
    DocumentRegular,
    DismissRegular,
} from '@fluentui/react-icons'
import type { ChatAttachment } from '../../models'
import { usePreferences } from '../../hooks'
import { appTokens } from '../../styles/appTokens'
import { InfoBadge } from '../shared/InfoBadge'

type AttachmentListItem = ChatAttachment & {
    isUploading?: boolean
}

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: appTokens.space.xs,
        paddingTop: appTokens.space.sm,
        paddingBottom: appTokens.space.sm,
        paddingLeft: appTokens.space.lg,
        paddingRight: appTokens.space.lg,
        borderTop: appTokens.border.subtle,
        backgroundColor: appTokens.color.surfaceAlt,
        flexShrink: 0,
    },
    containerCompact: {
        gap: appTokens.space.xxs,
        paddingTop: appTokens.space.xxs,
        paddingBottom: appTokens.space.xxs,
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
    },
    chip: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.xxs,
        backgroundColor: appTokens.color.pageBackground,
        borderRadius: appTokens.radius.md,
        paddingTop: appTokens.space.xxxs,
        paddingRight: appTokens.space.xs,
        paddingBottom: appTokens.space.xxxs,
        paddingLeft: appTokens.space.sm,
        maxWidth: '200px',
    },
    chipUploading: {
        backgroundColor: appTokens.color.surfaceRaised,
        border: `1px dashed ${appTokens.color.border}`,
    },
    chipCompact: {
        maxWidth: '170px',
        paddingTop: '0.0625rem',
        paddingBottom: '0.0625rem',
        paddingLeft: appTokens.space.xs,
        paddingRight: appTokens.space.xxs,
    },
    fileName: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    fileNameCompact: {
        fontSize: appTokens.fontSize.xs,
        lineHeight: appTokens.lineHeight.tight,
    },
})

interface AttachedFilesProps {
    attachments: AttachmentListItem[]
    onDelete: (id: string) => void
    deleting?: boolean
}

export function AttachedFiles({ attachments, onDelete, deleting }: AttachedFilesProps) {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isCompact = preferences?.compactMode ?? false

    if (attachments.length === 0) return null

    return (
        <div className={mergeClasses(styles.container, isCompact && styles.containerCompact)}>
            {attachments.map((a) => (
                <div
                    key={a.id}
                    className={mergeClasses(
                        styles.chip,
                        a.isUploading && styles.chipUploading,
                        isCompact && styles.chipCompact,
                    )}
                >
                    <DocumentRegular fontSize={isCompact ? 12 : 14} />
                    <Tooltip content={`${a.fileName} (${formatSize(a.contentLength)})`} relationship="description">
                        <Text size={200} className={mergeClasses(styles.fileName, isCompact && styles.fileNameCompact)}>{a.fileName}</Text>
                    </Tooltip>
                    {a.isUploading ? (
                        <Badge
                            appearance="filled"
                            size={isCompact ? 'tiny' : 'small'}
                            color="warning"
                        >
                            Uploading...
                        </Badge>
                    ) : (
                        <InfoBadge appearance="filled" size={isCompact ? 'tiny' : 'small'}>
                            {formatSize(a.contentLength)}
                        </InfoBadge>
                    )}
                    <Button
                        appearance="subtle"
                        size="small"
                        icon={<DismissRegular />}
                        onClick={() => onDelete(a.id)}
                        disabled={deleting || a.isUploading}
                        aria-label={`Remove ${a.fileName}`}
                    />
                </div>
            ))}
        </div>
    )
}

function formatSize(chars: number): string {
    if (chars < 1024) return `${chars} chars`
    return `${(chars / 1024).toFixed(1)} KB`
}
