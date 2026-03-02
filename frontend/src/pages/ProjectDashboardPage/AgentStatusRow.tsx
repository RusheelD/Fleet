import {
    makeStyles,
    tokens,
    Caption1,
    Text,
    Badge,
    ProgressBar,
} from '@fluentui/react-components'
import { BotRegular } from '@fluentui/react-icons'

const useStyles = makeStyles({
    agentRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.75rem',
    },
    agentBotIcon: {
        fontSize: '20px',
        color: tokens.colorBrandForeground1,
    },
    agentInfo: {
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        gap: '0.25rem',
    },
    agentNameRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
    },
})

interface AgentStatusRowProps {
    name: string
    status: string
    task: string
    progress: number
}

export function AgentStatusRow({ name, status, task, progress }: AgentStatusRowProps) {
    const styles = useStyles()

    return (
        <div className={styles.agentRow}>
            <BotRegular className={styles.agentBotIcon} />
            <div className={styles.agentInfo}>
                <div className={styles.agentNameRow}>
                    <Text weight="semibold">{name}</Text>
                    <Badge
                        appearance="filled"
                        color={status === 'running' ? 'success' : 'informative'}
                        size="small"
                    >
                        {status}
                    </Badge>
                </div>
                <Caption1>{task}</Caption1>
                {progress > 0 && (
                    <ProgressBar value={progress} thickness="medium" />
                )}
            </div>
        </div>
    )
}
