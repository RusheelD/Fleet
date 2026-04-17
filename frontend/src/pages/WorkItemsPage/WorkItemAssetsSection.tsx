import { Button, Caption1, Text } from '@fluentui/react-components'
import { DeleteRegular } from '@fluentui/react-icons'
import { useRef } from 'react'
import type { WorkItemAttachment } from '../../models'
import { AuthorizedAttachmentImage, AuthorizedAttachmentLink } from '../../components/shared/AuthorizedAttachment'

interface WorkItemAssetsSectionProps {
    attachments: WorkItemAttachment[]
    isLoading: boolean
    isUploading: boolean
    isDeleting: boolean
    errorMessage: string | null
    onUploadFiles: (files: File[]) => Promise<void>
    onInsertInDescription: (attachment: WorkItemAttachment) => void
    onInsertInCriteria: (attachment: WorkItemAttachment) => void
    onDelete: (attachmentId: string) => void
    formatAttachmentSize: (contentLength: number) => string
    sectionClassName: string
    headerClassName: string
    titleGroupClassName: string
    sectionTitleClassName: string
    hiddenInputClassName: string
    assetErrorClassName: string
    emptyAssetsClassName: string
    assetListClassName: string
    assetCardClassName: string
    assetPreviewClassName: string
    assetMetaClassName: string
    assetFileNameClassName: string
    assetActionsClassName: string
    assetLinkClassName: string
}

export function WorkItemAssetsSection({
    attachments,
    isLoading,
    isUploading,
    isDeleting,
    errorMessage,
    onUploadFiles,
    onInsertInDescription,
    onInsertInCriteria,
    onDelete,
    formatAttachmentSize,
    sectionClassName,
    headerClassName,
    titleGroupClassName,
    sectionTitleClassName,
    hiddenInputClassName,
    assetErrorClassName,
    emptyAssetsClassName,
    assetListClassName,
    assetCardClassName,
    assetPreviewClassName,
    assetMetaClassName,
    assetFileNameClassName,
    assetActionsClassName,
    assetLinkClassName,
}: WorkItemAssetsSectionProps) {
    const fileInputRef = useRef<HTMLInputElement | null>(null)

    return (
        <div className={sectionClassName}>
            <div className={headerClassName}>
                <div className={titleGroupClassName}>
                    <Text className={sectionTitleClassName}>Assets</Text>
                    <Caption1>
                        Upload images, documents, or other files here, then reference them in the description or acceptance criteria so Fleet builders can use them during runs.
                    </Caption1>
                </div>
                <Button
                    appearance="secondary"
                    onClick={() => fileInputRef.current?.click()}
                    disabled={isUploading}
                >
                    {isUploading ? 'Uploading...' : 'Upload assets'}
                </Button>
                <input
                    ref={fileInputRef}
                    className={hiddenInputClassName}
                    type="file"
                    multiple
                    onChange={(event) => {
                        const files = Array.from(event.target.files ?? [])
                        if (files.length > 0) {
                            void onUploadFiles(files)
                        }
                        event.target.value = ''
                    }}
                />
            </div>
            {errorMessage ? (
                <Caption1 className={assetErrorClassName}>
                    {errorMessage}
                </Caption1>
            ) : null}
            {isLoading ? (
                <Caption1 className={emptyAssetsClassName}>
                    Loading assets...
                </Caption1>
            ) : null}

            {!isLoading && attachments.length === 0 ? (
                <Caption1 className={emptyAssetsClassName}>
                    No assets uploaded to this work item yet.
                </Caption1>
            ) : (
                <div className={assetListClassName}>
                    {attachments.map((attachment) => (
                        <div key={attachment.id} className={assetCardClassName}>
                            {attachment.isImage && attachment.contentUrl ? (
                                <AuthorizedAttachmentImage
                                    src={attachment.contentUrl}
                                    alt={attachment.fileName}
                                    className={assetPreviewClassName}
                                />
                            ) : null}
                            <div className={assetMetaClassName}>
                                <Text className={assetFileNameClassName}>{attachment.fileName}</Text>
                                <Caption1>
                                    {formatAttachmentSize(attachment.contentLength)} {'\u00B7'} {attachment.contentType || 'application/octet-stream'}
                                </Caption1>
                            </div>
                            <div className={assetActionsClassName}>
                                <Button
                                    size="small"
                                    appearance="secondary"
                                    onClick={() => onInsertInDescription(attachment)}
                                >
                                    Insert in Description
                                </Button>
                                <Button
                                    size="small"
                                    appearance="secondary"
                                    onClick={() => onInsertInCriteria(attachment)}
                                >
                                    Insert in Criteria
                                </Button>
                                <AuthorizedAttachmentLink
                                    className={assetLinkClassName}
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
                                    disabled={isDeleting}
                                    onClick={() => onDelete(attachment.id)}
                                >
                                    Delete
                                </Button>
                            </div>
                        </div>
                    ))}
                </div>
            )}
        </div>
    )
}
