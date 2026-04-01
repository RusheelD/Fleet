import {
    makeStyles,
    mergeClasses,
} from '@fluentui/react-components'
import { appTokens } from '../../styles/appTokens'

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
        borderRadius: appTokens.radius.full,
        display: 'inline-block',
        flexShrink: 0,
    },
    priorityDotP1: {
        backgroundColor: appTokens.color.danger,
    },
    priorityDotP2: {
        backgroundColor: appTokens.color.warning,
    },
    priorityDotP3: {
        backgroundColor: appTokens.color.brand,
    },
    priorityDotP4: {
        backgroundColor: appTokens.color.textTertiary,
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
