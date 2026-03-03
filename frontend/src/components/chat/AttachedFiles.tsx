import {
    makeStyles,
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

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: '0.375rem',
        padding: '0.5rem 1rem',
        borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
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
    fileName: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
})

interface AttachedFilesProps {
    attachments: ChatAttachment[]
    onDelete: (id: string) => void
    deleting?: boolean
}

export function AttachedFiles({ attachments, onDelete, deleting }: AttachedFilesProps) {
    const styles = useStyles()

    if (attachments.length === 0) return null

    return (
        <div className={styles.container}>
            {attachments.map((a) => (
                <div key={a.id} className={styles.chip}>
                    <DocumentRegular fontSize={14} />
                    <Tooltip content={`${a.fileName} (${formatSize(a.contentLength)})`} relationship="description">
                        <Text size={200} className={styles.fileName}>{a.fileName}</Text>
                    </Tooltip>
                    <Badge appearance="filled" size="small" color="informative">
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
