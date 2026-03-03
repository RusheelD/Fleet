import {
    makeStyles,
    tokens,
    Title3,
    Button,
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

const useStyles = makeStyles({
    logPanel: {
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
        backgroundColor: tokens.colorNeutralBackground1,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: tokens.borderRadiusMedium,
    },
    logHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: '0.75rem 1rem',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    logTitle: {
        fontSize: '14px',
    },
    logList: {
        flex: 1,
        overflow: 'auto',
        padding: '0.5rem',
        fontFamily: 'Consolas, "Courier New", monospace',
        fontSize: '12px',
    },
    logEntry: {
        display: 'grid',
        gridTemplateColumns: '90px 80px auto',
        gap: '0.5rem',
        padding: '0.25rem 0.5rem',
        borderRadius: tokens.borderRadiusSmall,
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground3,
        },
    },
    logTime: {
        color: tokens.colorNeutralForeground4,
    },
    logAgent: {
        fontWeight: 600,
        color: tokens.colorBrandForeground1,
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
})

interface LogPanelProps {
    logs: LogEntry[]
    onRefresh?: () => void
}

export function LogPanel({ logs, onRefresh }: LogPanelProps) {
    const styles = useStyles()

    return (
        <div className={styles.logPanel}>
            <div className={styles.logHeader}>
                <Title3 className={styles.logTitle}>Live Logs</Title3>
                <Button appearance="subtle" size="small" icon={<ArrowClockwiseRegular />} aria-label="Refresh logs" onClick={onRefresh} />
            </div>
            <div className={styles.logList}>
                {logs.map((log, i) => (
                    <div key={i} className={styles.logEntry}>
                        <span className={styles.logTime}>{log.time}</span>
                        <span className={styles.logAgent}>[{log.agent}]</span>
                        <span className={mergeClasses(styles.logMessage, styles[LOG_LEVEL_CLASSES[log.level]])}>{log.message}</span>
                    </div>
                ))}
            </div>
        </div>
    )
}
