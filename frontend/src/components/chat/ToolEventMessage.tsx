import {
    makeStyles,
    tokens,
    Caption1,
    Text,
} from '@fluentui/react-components'
import { WrenchRegular } from '@fluentui/react-icons'
import type { ToolEvent } from '../../models'

const useStyles = makeStyles({
    container: {
        display: 'flex',
        gap: '0.5rem',
        alignItems: 'flex-start',
        padding: '0.5rem 0.75rem',
        borderRadius: tokens.borderRadiusMedium,
        backgroundColor: tokens.colorNeutralBackground4,
        fontSize: '12px',
    },
    icon: {
        color: tokens.colorBrandForeground1,
        fontSize: '14px',
        marginTop: '2px',
        flexShrink: 0,
    },
    content: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0.125rem',
        minWidth: 0,
    },
    toolName: {
        fontFamily: 'monospace',
        fontWeight: 600,
    },
    result: {
        color: tokens.colorNeutralForeground3,
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
        maxHeight: '100px',
        overflow: 'hidden',
    },
})

interface ToolEventMessageProps {
    event: ToolEvent
}

export function ToolEventMessage({ event }: ToolEventMessageProps) {
    const styles = useStyles()

    const displayName = event.toolName.replace(/_/g, ' ')

    return (
        <div className={styles.container}>
            <WrenchRegular className={styles.icon} />
            <div className={styles.content}>
                <Text size={200}>
                    Used tool: <span className={styles.toolName}>{displayName}</span>
                </Text>
                {event.result && (
                    <Caption1 className={styles.result}>
                        {event.result.length > 200
                            ? event.result.slice(0, 200) + '...'
                            : event.result}
                    </Caption1>
                )}
            </div>
        </div>
    )
}
