import {
    makeStyles,
} from '@fluentui/react-components'
import { appTokens } from '../../styles/appTokens'

const STATE_DOT_COLORS: Record<string, string> = {
    'New': appTokens.color.textTertiary,
    'Active': appTokens.color.brand,
    'In Progress': appTokens.color.warning,
    'In Progress (AI)': appTokens.color.warning,
    'In-PR': appTokens.color.accentOrange,
    'In-PR (AI)': appTokens.color.accentOrange,
    'Resolved': appTokens.color.success,
    'Resolved (AI)': appTokens.color.success,
    'Closed': appTokens.color.success,
}

const useStyles = makeStyles({
    dot: {
        width: '10px',
        height: '10px',
        borderRadius: appTokens.radius.full,
        display: 'inline-block',
        flexShrink: 0,
        borderTopWidth: '1.5px',
        borderRightWidth: '1.5px',
        borderBottomWidth: '1.5px',
        borderLeftWidth: '1.5px',
        borderTopStyle: 'solid',
        borderRightStyle: 'solid',
        borderBottomStyle: 'solid',
        borderLeftStyle: 'solid',
        borderTopColor: 'transparent',
        borderRightColor: 'transparent',
        borderBottomColor: 'transparent',
        borderLeftColor: 'transparent',
    },
})

interface StateDotProps {
    state: string
}

export function StateDot({ state }: StateDotProps) {
    const styles = useStyles()
    const color = STATE_DOT_COLORS[state] ?? appTokens.color.textTertiary

    return (
        <span
            className={styles.dot}
            style={{
                backgroundColor: state === 'New' ? 'transparent' : color,
                borderColor: color,
            }}
        />
    )
}
