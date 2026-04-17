import { Text, Textarea, mergeClasses } from '@fluentui/react-components'
import Markdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { AuthorizedAttachmentImage, AuthorizedAttachmentLink } from '../../components/shared/AuthorizedAttachment'

interface WorkItemMarkdownSectionProps {
    title: string
    value: string
    onChange: (value: string) => void
    placeholder: string
    sectionClassName: string
    sectionMobileClassName?: string
    textareaClassName?: string
    textareaMobileClassName?: string
    titleClassName: string
    previewClassName: string
    isMobile: boolean
    rows?: number
}

export function WorkItemMarkdownSection({
    title,
    value,
    onChange,
    placeholder,
    sectionClassName,
    sectionMobileClassName,
    textareaClassName,
    textareaMobileClassName,
    titleClassName,
    previewClassName,
    isMobile,
    rows,
}: WorkItemMarkdownSectionProps) {
    return (
        <div className={mergeClasses(sectionClassName, isMobile && sectionMobileClassName)}>
            <Text className={titleClassName}>{title}</Text>
            <Textarea
                className={mergeClasses(textareaClassName, isMobile && textareaMobileClassName)}
                value={value}
                onChange={(_event, data) => onChange(data.value)}
                resize="vertical"
                rows={rows}
                placeholder={placeholder}
            />
            {value.trim().length > 0 && (
                <div className={previewClassName}>
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
                        {value}
                    </Markdown>
                </div>
            )}
        </div>
    )
}
