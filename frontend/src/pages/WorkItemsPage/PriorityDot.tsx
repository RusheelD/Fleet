import {
    makeStyles,
    tokens,
    mergeClasses,
} from '@fluentui/react-components'

const PRIORITY_DOT_CLASSES: Record<number, 'priorityDotP1' | 'priorityDotP2' | 'priorityDotP3' | 'priorityDotP4'> = {
    1: 'priorityDotP1',
    2: 'priorityDotP2',
    3: 'priorityDotP3',
    4: 'priorityDotP4',
}

const useStyles = makeStyles({
    priorityDot: {
        width: '8px',
        height: '8px',
        borderRadius: '50%',
        display: 'inline-block',
        flexShrink: 0,
    },
    priorityDotP1: {
        backgroundColor: tokens.colorPaletteRedForeground1,
    },
    priorityDotP2: {
        backgroundColor: tokens.colorPaletteMarigoldForeground1,
    },
    priorityDotP3: {
        backgroundColor: tokens.colorBrandForeground1,
    },
    priorityDotP4: {
        backgroundColor: tokens.colorNeutralForeground3,
    },
})

interface PriorityDotProps {
    priority: number
}

export function PriorityDot({ priority }: PriorityDotProps) {
    const styles = useStyles()
    const variant = PRIORITY_DOT_CLASSES[priority]

    return (
        <span
            className={mergeClasses(styles.priorityDot, variant ? styles[variant] : undefined)}
        />
    )
}
