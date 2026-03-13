import {
    makeStyles,
    mergeClasses,
    tokens,
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

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: '0.375rem',
        padding: '0.5rem 1rem',
        borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: tokens.colorNeutralBackground2,
        flexShrink: 0,
    },
    containerCompact: {
        gap: '0.25rem',
        paddingTop: '0.25rem',
        paddingBottom: '0.25rem',
        paddingLeft: '0.5rem',
        paddingRight: '0.5rem',
    },
    chip: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.25rem',
        backgroundColor: tokens.colorNeutralBackground3,
        borderRadius: tokens.borderRadiusMedium,
        padding: '0.125rem 0.375rem 0.125rem 0.5rem',
        maxWidth: '200px',
    },
    chipCompact: {
        maxWidth: '170px',
        paddingTop: '0.0625rem',
        paddingBottom: '0.0625rem',
        paddingLeft: '0.375rem',
        paddingRight: '0.25rem',
    },
    fileName: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    fileNameCompact: {
        fontSize: '11px',
        lineHeight: '14px',
    },
})

interface AttachedFilesProps {
    attachments: ChatAttachment[]
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
                <div key={a.id} className={mergeClasses(styles.chip, isCompact && styles.chipCompact)}>
                    <DocumentRegular fontSize={isCompact ? 12 : 14} />
                    <Tooltip content={`${a.fileName} (${formatSize(a.contentLength)})`} relationship="description">
                        <Text size={200} className={mergeClasses(styles.fileName, isCompact && styles.fileNameCompact)}>{a.fileName}</Text>
                    </Tooltip>
                    <Badge appearance="filled" size={isCompact ? 'tiny' : 'small'} color="informative">
                        {formatSize(a.contentLength)}
                    </Badge>
                    <Button
                        appearance="subtle"
                        size="small"
                        icon={<DismissRegular />}
                        onClick={() => onDelete(a.id)}
                        disabled={deleting}
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
