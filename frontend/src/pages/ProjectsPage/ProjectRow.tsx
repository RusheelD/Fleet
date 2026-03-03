import {
    makeStyles,
    tokens,
    Caption1,
    Text,
    Badge,
} from '@fluentui/react-components'
import {
    FolderRegular,
    BotRegular,
    ClockRegular,
} from '@fluentui/react-icons'
import type { ProjectData } from '../../models'

const useStyles = makeStyles({
    row: {
        display: 'grid',
        gridTemplateColumns: '2fr 1fr 80px 80px 80px 100px 140px',
        alignItems: 'center',
        padding: '0.5rem 0.75rem',
        gap: '0.75rem',
        borderRadius: tokens.borderRadiusMedium,
        cursor: 'pointer',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },
    nameCell: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0.125rem',
        minWidth: 0,
    },
    title: {
        fontWeight: 600,
        fontSize: '13px',
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    description: {
        color: tokens.colorNeutralForeground3,
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    repoCell: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.375rem',
        minWidth: 0,
        color: tokens.colorNeutralForeground3,
    },
    repoText: {
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    statCell: {
        textAlign: 'center',
        fontVariantNumeric: 'tabular-nums',
    },
    agentCell: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        gap: '0.375rem',
    },
    activityCell: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.375rem',
        color: tokens.colorNeutralForeground4,
    },
    clockIcon: {
        fontSize: '12px',
        flexShrink: 0,
    },
})

interface ProjectRowProps {
    project: ProjectData
    onClick: () => void
}

export function ProjectRow({ project, onClick }: ProjectRowProps) {
    const styles = useStyles()

    return (
        <div className={styles.row} onClick={onClick}>
            <div className={styles.nameCell}>
                <Text className={styles.title}>{project.title}</Text>
                <Caption1 className={styles.description}>{project.description}</Caption1>
            </div>
            <div className={styles.repoCell}>
                <FolderRegular />
                <Caption1 className={styles.repoText}>{project.repo}</Caption1>
            </div>
            <Text className={styles.statCell}>{project.workItems.total}</Text>
            <Text className={styles.statCell}>{project.workItems.active}</Text>
            <Text className={styles.statCell}>{project.workItems.resolved}</Text>
            <div className={styles.agentCell}>
                <BotRegular />
                {project.agents.running > 0 ? (
                    <Badge appearance="filled" color="success" size="tiny">
                        {project.agents.running}
                    </Badge>
                ) : (
                    <Caption1>0</Caption1>
                )}
            </div>
            <div className={styles.activityCell}>
                <ClockRegular className={styles.clockIcon} />
                <Caption1>{project.lastActivity}</Caption1>
            </div>
        </div>
    )
}
