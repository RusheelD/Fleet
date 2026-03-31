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
    tokens,
} from '@fluentui/react-components'
import Markdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import type { ExecutionDocumentation } from '../../proxies'
import { useIsMobile } from '../../hooks'
import { downloadExecutionDocumentation, normalizeExecutionDocumentationMarkdown } from './executionDocs'

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
        gap: tokens.spacingVerticalM,
        minHeight: 0,
        overflow: 'hidden',
    },
    dialogContentMobile: {
        gap: tokens.spacingVerticalS,
    },
    docMeta: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalM,
        flexWrap: 'wrap',
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
    },
    docLinkRow: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalM,
        flexWrap: 'wrap',
    },
    markdownFrame: {
        flex: 1,
        minHeight: 0,
        maxHeight: 'calc(100vh - 16rem)',
        overflowY: 'auto',
        borderTopWidth: '1px',
        borderRightWidth: '1px',
        borderBottomWidth: '1px',
        borderLeftWidth: '1px',
        borderTopStyle: 'solid',
        borderRightStyle: 'solid',
        borderBottomStyle: 'solid',
        borderLeftStyle: 'solid',
        borderTopColor: tokens.colorNeutralStroke2,
        borderRightColor: tokens.colorNeutralStroke2,
        borderBottomColor: tokens.colorNeutralStroke2,
        borderLeftColor: tokens.colorNeutralStroke2,
        borderRadius: tokens.borderRadiusLarge,
        backgroundColor: tokens.colorNeutralBackground1,
        paddingTop: tokens.spacingVerticalM,
        paddingBottom: tokens.spacingVerticalM,
        paddingLeft: tokens.spacingHorizontalL,
        paddingRight: tokens.spacingHorizontalL,
    },
    markdownFrameMobile: {
        maxHeight: 'calc(100vh - 14rem)',
        paddingTop: tokens.spacingVerticalS,
        paddingBottom: tokens.spacingVerticalS,
        paddingLeft: tokens.spacingHorizontalM,
        paddingRight: tokens.spacingHorizontalM,
    },
    markdown: {
        color: tokens.colorNeutralForeground1,
        lineHeight: tokens.lineHeightBase400,
        '& > :first-child': {
            marginTop: 0,
        },
        '& > :last-child': {
            marginBottom: 0,
        },
        '& h1, & h2, & h3, & h4': {
            marginTop: tokens.spacingVerticalL,
            marginBottom: tokens.spacingVerticalS,
            lineHeight: tokens.lineHeightBase500,
        },
        '& p, & ul, & ol, & blockquote': {
            marginTop: 0,
            marginBottom: tokens.spacingVerticalM,
        },
        '& ul, & ol': {
            paddingLeft: '1.5rem',
        },
        '& li + li': {
            marginTop: tokens.spacingVerticalXXS,
        },
        '& code': {
            fontFamily: tokens.fontFamilyMonospace,
            backgroundColor: tokens.colorNeutralBackground3,
            borderRadius: tokens.borderRadiusSmall,
            paddingTop: '1px',
            paddingBottom: '1px',
            paddingLeft: '4px',
            paddingRight: '4px',
            fontSize: tokens.fontSizeBase200,
        },
        '& pre': {
            overflowX: 'auto',
            backgroundColor: tokens.colorNeutralBackground2,
            borderRadius: tokens.borderRadiusMedium,
            paddingTop: tokens.spacingVerticalS,
            paddingBottom: tokens.spacingVerticalS,
            paddingLeft: tokens.spacingHorizontalM,
            paddingRight: tokens.spacingHorizontalM,
            marginTop: 0,
            marginBottom: tokens.spacingVerticalM,
        },
        '& pre code': {
            backgroundColor: 'transparent',
            padding: 0,
        },
        '& blockquote': {
            borderLeftWidth: '3px',
            borderLeftStyle: 'solid',
            borderLeftColor: tokens.colorBrandStroke1,
            marginLeft: 0,
            paddingLeft: tokens.spacingHorizontalM,
            color: tokens.colorNeutralForeground3,
        },
        '& table': {
            width: '100%',
            borderCollapse: 'collapse',
            marginBottom: tokens.spacingVerticalM,
        },
        '& th, & td': {
            borderBottomWidth: '1px',
            borderBottomStyle: 'solid',
            borderBottomColor: tokens.colorNeutralStroke2,
            textAlign: 'left',
            paddingTop: tokens.spacingVerticalXS,
            paddingBottom: tokens.spacingVerticalXS,
            paddingLeft: tokens.spacingHorizontalS,
            paddingRight: tokens.spacingHorizontalS,
        },
    },
    dialogActionsMobile: {
        width: '100%',
        justifyContent: 'stretch',
        display: 'grid',
        gap: tokens.spacingVerticalXS,
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
