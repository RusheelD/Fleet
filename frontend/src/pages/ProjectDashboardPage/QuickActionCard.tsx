import {
    makeStyles,
    tokens,
    Caption1,
    Text,
    Card,
} from '@fluentui/react-components'
import type { ReactNode } from 'react'

const useStyles = makeStyles({
    quickActionCard: {
        padding: '1rem',
        display: 'flex',
        alignItems: 'center',
        gap: '0.75rem',
        cursor: 'pointer',
        ':hover': {
            boxShadow: tokens.shadow4,
        },
    },
    quickActionIcon: {
        fontSize: '24px',
        color: tokens.colorBrandForeground1,
    },
})

interface QuickActionCardProps {
    icon: ReactNode
    title: string
    description: string
    onClick?: () => void
}

export function QuickActionCard({ icon, title, description, onClick }: QuickActionCardProps) {
    const styles = useStyles()

    return (
        <Card className={styles.quickActionCard} onClick={onClick}>
            <span className={styles.quickActionIcon}>{icon}</span>
            <div>
                <Text weight="semibold" block>{title}</Text>
                <Caption1>{description}</Caption1>
            </div>
        </Card>
    )
}
