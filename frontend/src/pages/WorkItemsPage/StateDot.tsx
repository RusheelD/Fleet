import {
    makeStyles,
    tokens,
} from '@fluentui/react-components'

const STATE_DOT_COLORS: Record<string, string> = {
    'New': tokens.colorNeutralForeground3,
    'Active': tokens.colorCompoundBrandForeground1,
    'In Progress': tokens.colorPaletteMarigoldForeground1,
    'In Progress (AI)': tokens.colorPaletteMarigoldForeground1,
    'Resolved': tokens.colorPaletteGreenForeground1,
    'Resolved (AI)': tokens.colorPaletteGreenForeground1,
    'Closed': tokens.colorPaletteGreenForeground1,
}

const useStyles = makeStyles({
    dot: {
        width: '10px',
        height: '10px',
        borderRadius: '50%',
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
    const color = STATE_DOT_COLORS[state] ?? tokens.colorNeutralForeground3

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
