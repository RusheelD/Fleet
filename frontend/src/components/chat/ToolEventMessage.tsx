import {
    makeStyles,
    Caption1,
    Text,
} from '@fluentui/react-components'
import { WrenchRegular } from '@fluentui/react-icons'
import type { ToolEvent } from '../../models'
import { appTokens } from '../../styles/appTokens'

const useStyles = makeStyles({
    container: {
        display: 'flex',
        gap: appTokens.space.sm,
        alignItems: 'flex-start',
        paddingTop: appTokens.space.sm,
        paddingBottom: appTokens.space.sm,
        paddingLeft: appTokens.space.md,
        paddingRight: appTokens.space.md,
        borderRadius: appTokens.radius.md,
        backgroundColor: appTokens.color.surfaceRaised,
        fontSize: appTokens.fontSize.sm,
    },
    icon: {
        color: appTokens.color.brand,
        fontSize: appTokens.fontSize.sm,
        marginTop: '2px',
        flexShrink: 0,
    },
    content: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxxs,
        minWidth: 0,
    },
    toolName: {
        fontFamily: 'monospace',
        fontWeight: appTokens.fontWeight.semibold,
    },
    result: {
        color: appTokens.color.textTertiary,
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
