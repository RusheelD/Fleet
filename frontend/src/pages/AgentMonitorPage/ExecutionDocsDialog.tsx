import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    Link,
    makeStyles,
    mergeClasses,
} from '@fluentui/react-components'
import Markdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import type { ExecutionDocumentation } from '../../proxies'
import { useIsMobile } from '../../hooks'
import { downloadExecutionDocumentation, normalizeExecutionDocumentationMarkdown } from './executionDocs'
import { appTokens } from '../../styles/appTokens'

const useStyles = makeStyles({
    dialogSurface: {
        width: 'min(960px, calc(100vw - 2rem))',
        maxHeight: 'calc(100vh - 2rem)',
    },
    dialogSurfaceMobile: {
        width: 'calc(100vw - 0.75rem)',
        maxHeight: 'calc(100vh - 0.75rem)',
    },
    dialogBody: {
        overflow: 'hidden',
    },
    dialogContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.md,
        minHeight: 0,
        overflow: 'hidden',
    },
    dialogContentMobile: {
        gap: appTokens.space.sm,
    },
    docMeta: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.md,
        flexWrap: 'wrap',
        color: appTokens.color.textTertiary,
        fontSize: appTokens.fontSize.sm,
    },
    docLinkRow: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.md,
        flexWrap: 'wrap',
    },
    markdownFrame: {
        flex: 1,
        minHeight: 0,
        maxHeight: 'calc(100vh - 16rem)',
        overflowY: 'auto',
        border: appTokens.border.subtle,
        borderRadius: appTokens.radius.lg,
        backgroundColor: appTokens.color.surface,
        paddingTop: appTokens.space.md,
        paddingBottom: appTokens.space.md,
        paddingLeft: appTokens.space.xl,
        paddingRight: appTokens.space.xl,
    },
    markdownFrameMobile: {
        maxHeight: 'calc(100vh - 14rem)',
        paddingTop: appTokens.space.sm,
        paddingBottom: appTokens.space.sm,
        paddingLeft: appTokens.space.lg,
        paddingRight: appTokens.space.lg,
    },
    markdown: {
        color: appTokens.color.textPrimary,
        lineHeight: appTokens.lineHeight.base,
        '& > :first-child': {
            marginTop: 0,
        },
        '& > :last-child': {
            marginBottom: 0,
        },
        '& h1, & h2, & h3, & h4': {
            marginTop: appTokens.space.xl,
            marginBottom: appTokens.space.sm,
            lineHeight: appTokens.lineHeight.relaxed,
        },
        '& p, & ul, & ol, & blockquote': {
            marginTop: 0,
            marginBottom: appTokens.space.md,
        },
        '& ul, & ol': {
            paddingLeft: '1.5rem',
        },
        '& li + li': {
            marginTop: appTokens.space.xxxs,
        },
        '& code': {
            fontFamily: 'monospace',
            backgroundColor: appTokens.color.pageBackground,
            borderRadius: appTokens.radius.sm,
            paddingTop: '1px',
            paddingBottom: '1px',
            paddingLeft: '4px',
            paddingRight: '4px',
            fontSize: appTokens.fontSize.sm,
        },
        '& pre': {
            overflowX: 'auto',
            backgroundColor: appTokens.color.surfaceAlt,
            borderRadius: appTokens.radius.md,
            paddingTop: appTokens.space.sm,
            paddingBottom: appTokens.space.sm,
            paddingLeft: appTokens.space.lg,
            paddingRight: appTokens.space.lg,
            marginTop: 0,
            marginBottom: appTokens.space.md,
        },
        '& pre code': {
            backgroundColor: 'transparent',
            padding: 0,
        },
        '& blockquote': {
            borderLeftWidth: '3px',
            borderLeftStyle: 'solid',
            borderLeftColor: appTokens.color.brandStroke,
            marginLeft: 0,
            paddingLeft: appTokens.space.lg,
            color: appTokens.color.textTertiary,
        },
        '& table': {
            width: '100%',
            borderCollapse: 'collapse',
            marginBottom: appTokens.space.md,
        },
        '& th, & td': {
            borderBottomWidth: '1px',
            borderBottomStyle: 'solid',
            borderBottomColor: appTokens.color.border,
            textAlign: 'left',
            paddingTop: appTokens.space.xs,
            paddingBottom: appTokens.space.xs,
            paddingLeft: appTokens.space.sm,
            paddingRight: appTokens.space.sm,
        },
    },
    dialogActionsMobile: {
        width: '100%',
        justifyContent: 'stretch',
        display: 'grid',
        gap: appTokens.space.xs,
    },
    dialogActionButtonMobile: {
        width: '100%',
    },
})

interface ExecutionDocsDialogProps {
    docs: ExecutionDocumentation | null
    open: boolean
    onOpenChange: (open: boolean) => void
}

export function ExecutionDocsDialog({ docs, open, onOpenChange }: ExecutionDocsDialogProps) {
    const styles = useStyles()
    const isMobile = useIsMobile()
    const renderedMarkdown = docs ? normalizeExecutionDocumentationMarkdown(docs.markdown) : ''

    return (
        <Dialog open={open} onOpenChange={(_event, data) => onOpenChange(data.open)}>
            <DialogSurface className={mergeClasses(styles.dialogSurface, isMobile && styles.dialogSurfaceMobile)}>
                <DialogBody className={styles.dialogBody}>
                    <DialogTitle>{docs?.title ?? 'Execution Documentation'}</DialogTitle>
                    <DialogContent className={mergeClasses(styles.dialogContent, isMobile && styles.dialogContentMobile)}>
                        {docs ? (
                            <>
                                <div className={styles.docMeta}>
                                    <span>Execution {docs.executionId}</span>
                                    <div className={styles.docLinkRow}>
                                        {docs.pullRequestUrl && (
                                            <Link href={docs.pullRequestUrl} target="_blank" rel="noopener noreferrer">
                                                Pull Request
                                            </Link>
                                        )}
                                        {docs.diffUrl && (
                                            <Link href={docs.diffUrl} target="_blank" rel="noopener noreferrer">
                                                Diff
                                            </Link>
                                        )}
                                    </div>
                                </div>
                                <div className={mergeClasses(styles.markdownFrame, isMobile && styles.markdownFrameMobile)}>
                                    <div className={styles.markdown}>
                                        <Markdown
                                            remarkPlugins={[remarkGfm]}
                                            components={{
                                                a: ({ children, href }) => (
                                                    <Link href={href ?? '#'} target="_blank" rel="noopener noreferrer" inline>
                                                        {children}
                                                    </Link>
                                                ),
                                            }}
                                        >
                                            {renderedMarkdown}
                                        </Markdown>
                                    </div>
                                </div>
                            </>
                        ) : null}
                    </DialogContent>
                    <DialogActions className={mergeClasses(isMobile && styles.dialogActionsMobile)}>
                        <Button
                            appearance="secondary"
                            onClick={() => onOpenChange(false)}
                            className={mergeClasses(isMobile && styles.dialogActionButtonMobile)}
                        >
                            Close
                        </Button>
                        <Button
                            appearance="primary"
                            onClick={() => {
                                if (docs) {
                                    downloadExecutionDocumentation(docs)
                                }
                            }}
                            disabled={!docs}
                            className={mergeClasses(isMobile && styles.dialogActionButtonMobile)}
                        >
                            Download .md
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    )
}
