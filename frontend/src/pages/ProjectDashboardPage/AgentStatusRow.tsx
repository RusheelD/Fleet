import {
    makeStyles,
    mergeClasses,
    Caption1,
    Text,
    Badge,
    ProgressBar,
} from '@fluentui/react-components'
import { BotRegular } from '@fluentui/react-icons'
import { useIsMobile } from '../../hooks'
import { appTokens } from '../../styles/appTokens'

const useStyles = makeStyles({
    agentRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.75rem',
        minWidth: 0,
    },
    agentRowMobile: {
        alignItems: 'flex-start',
    },
    agentBotIcon: {
        fontSize: '20px',
        color: appTokens.color.brand,
    },
    agentInfo: {
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        gap: '0.25rem',
        minWidth: 0,
    },
    agentNameRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
        flexWrap: 'wrap',
        minWidth: 0,
    },
    taskText: {
        overflowWrap: 'anywhere',
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
    const isMobile = useIsMobile()

    return (
        <div className={mergeClasses(styles.agentRow, isMobile && styles.agentRowMobile)}>
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
                <Caption1 className={styles.taskText}>{task}</Caption1>
                {progress > 0 && (
                    <ProgressBar value={progress} thickness="medium" />
                )}
            </div>
        </div>
    )
}
