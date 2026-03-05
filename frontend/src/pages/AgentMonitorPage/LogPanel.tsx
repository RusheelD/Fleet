import {
    makeStyles,
    tokens,
    Title3,
    Button,
    Badge,
    mergeClasses,
} from '@fluentui/react-components'
import { ArrowClockwiseRegular } from '@fluentui/react-icons'
import type { LogEntry } from '../../models'

const LOG_LEVEL_CLASSES: Record<string, 'logLevelInfo' | 'logLevelWarn' | 'logLevelError' | 'logLevelSuccess'> = {
    info: 'logLevelInfo',
    warn: 'logLevelWarn',
    error: 'logLevelError',
    success: 'logLevelSuccess',
}

/** Format an ISO string to HH:MM:SS (24-hour) for the log gutter. */
function formatLogTime(iso: string): string {
    try {
        const d = new Date(iso)
        return d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false })
    } catch {
        return iso
    }
}

const useStyles = makeStyles({
    logPanel: {
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
        backgroundColor: tokens.colorNeutralBackground1,
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
        borderRadius: tokens.borderRadiusMedium,
    },
    logHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        paddingTop: tokens.spacingVerticalS,
        paddingBottom: tokens.spacingVerticalS,
        paddingLeft: tokens.spacingHorizontalL,
        paddingRight: tokens.spacingHorizontalL,
        borderBottomWidth: '1px',
        borderBottomStyle: 'solid',
        borderBottomColor: tokens.colorNeutralStroke2,
    },
    logTitle: {
        fontSize: '14px',
    },
    logList: {
        flex: 1,
        overflow: 'auto',
        paddingTop: tokens.spacingVerticalXS,
        paddingBottom: tokens.spacingVerticalXS,
        paddingLeft: tokens.spacingHorizontalS,
        paddingRight: tokens.spacingHorizontalS,
        fontFamily: '"Cascadia Code", Consolas, "Courier New", monospace',
        fontSize: '12px',
        lineHeight: '20px',
    },
    logEntry: {
        display: 'grid',
        gridTemplateColumns: '62px auto 14px 1fr',
        gap: tokens.spacingHorizontalM,
        alignItems: 'baseline',
        paddingTop: tokens.spacingVerticalXS,
        paddingBottom: tokens.spacingVerticalXS,
        paddingLeft: tokens.spacingHorizontalS,
        paddingRight: tokens.spacingHorizontalS,
        borderRadius: tokens.borderRadiusSmall,
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground3,
        },
    },
    logTime: {
        color: tokens.colorNeutralForeground4,
        fontVariantNumeric: 'tabular-nums',
        whiteSpace: 'nowrap',
    },
    logAgent: {
        fontWeight: 600,
        color: tokens.colorBrandForeground1,
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    levelDot: {
        display: 'inline-block',
        width: '8px',
        height: '8px',
        borderRadius: '50%',
        flexShrink: 0,
        alignSelf: 'center',
    },
    levelDotInfo: {
        backgroundColor: tokens.colorNeutralForeground4,
    },
    levelDotWarn: {
        backgroundColor: tokens.colorPaletteMarigoldForeground1,
    },
    levelDotError: {
        backgroundColor: tokens.colorPaletteRedForeground1,
    },
    levelDotSuccess: {
        backgroundColor: tokens.colorPaletteGreenForeground1,
    },
    logMessage: {
        wordBreak: 'break-word',
    },
    logLevelInfo: {
        color: tokens.colorNeutralForeground1,
    },
    logLevelWarn: {
        color: tokens.colorPaletteMarigoldForeground1,
    },
    logLevelError: {
        color: tokens.colorPaletteRedForeground1,
    },
    logLevelSuccess: {
        color: tokens.colorPaletteGreenForeground1,
    },
    emptyState: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        flex: 1,
        color: tokens.colorNeutralForeground4,
        fontStyle: 'italic',
        paddingTop: tokens.spacingVerticalXXL,
        paddingBottom: tokens.spacingVerticalXXL,
    },
})

const LEVEL_DOT_CLASSES: Record<string, 'levelDotInfo' | 'levelDotWarn' | 'levelDotError' | 'levelDotSuccess'> = {
    info: 'levelDotInfo',
    warn: 'levelDotWarn',
    error: 'levelDotError',
    success: 'levelDotSuccess',
}

interface LogPanelProps {
    logs: LogEntry[]
    onRefresh?: () => void
}

export function LogPanel({ logs, onRefresh }: LogPanelProps) {
    const styles = useStyles()

    return (
        <div className={styles.logPanel}>
            <div className={styles.logHeader}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <Title3 className={styles.logTitle}>Live Logs</Title3>
                    <Badge appearance="filled" color="informative" size="small">{logs.length}</Badge>
                </div>
                <Button appearance="subtle" size="small" icon={<ArrowClockwiseRegular />} aria-label="Refresh logs" onClick={onRefresh} />
            </div>
            <div className={styles.logList}>
                {logs.length === 0 && (
                    <div className={styles.emptyState}>No log entries yet</div>
                )}
                {logs.map((log, i) => (
                    <div key={i} className={styles.logEntry}>
                        <span className={styles.logTime}>{formatLogTime(log.time)}</span>
                        <span className={styles.logAgent}>{log.agent}</span>
                        <span className={mergeClasses(styles.levelDot, styles[LEVEL_DOT_CLASSES[log.level]])} />
                        <span className={mergeClasses(styles.logMessage, styles[LOG_LEVEL_CLASSES[log.level]])}>{log.message}</span>
                    </div>
                ))}
            </div>
        </div>
    )
}
