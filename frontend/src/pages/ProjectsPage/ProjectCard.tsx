import {
    makeStyles,
    tokens,
    Title3,
    Caption1,
    Text,
    Card,
    CardHeader,
    CardPreview,
    Badge,
} from '@fluentui/react-components'
import {
    FolderRegular,
    ClockRegular,
    BotRegular,
} from '@fluentui/react-icons'
import type { ProjectData } from '../../models'

const useStyles = makeStyles({
    projectCard: {
        cursor: 'pointer',
        ':hover': {
            boxShadow: tokens.shadow8,
        },
    },
    cardPreview: {
        padding: '0 1rem 1rem',
        display: 'flex',
        flexDirection: 'column',
        gap: '0.75rem',
    },
    repoRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
        color: tokens.colorNeutralForeground3,
    },
    statsRow: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr 1fr',
        gap: '0.5rem',
        textAlign: 'center',
    },
    stat: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: '0.125rem',
    },
    statValue: {
        fontWeight: 700,
        fontSize: '18px',
    },
    statLabel: {
        fontSize: '12px',
        color: tokens.colorNeutralForeground4,
    },
    activityRow: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    agentBadge: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
    },
    clockIcon: {
        fontSize: '12px',
        color: tokens.colorNeutralForeground4,
    },
})

interface ProjectCardProps {
    project: ProjectData
    onClick: () => void
}

export function ProjectCard({ project, onClick }: ProjectCardProps) {
    const styles = useStyles()

    return (
        <Card className={styles.projectCard} onClick={onClick}>
            <CardHeader
                header={<Title3>{project.title}</Title3>}
                description={<Caption1>{project.description}</Caption1>}
            />
            <CardPreview className={styles.cardPreview}>
                <div className={styles.repoRow}>
                    <FolderRegular />
                    <Text size={200}>{project.repo}</Text>
                </div>

                <div className={styles.statsRow}>
                    <div className={styles.stat}>
                        <Text className={styles.statValue}>{project.workItems.total}</Text>
                        <Text className={styles.statLabel}>items</Text>
                    </div>
                    <div className={styles.stat}>
                        <Text className={styles.statValue}>{project.workItems.active}</Text>
                        <Text className={styles.statLabel}>active</Text>
                    </div>
                    <div className={styles.stat}>
                        <Text className={styles.statValue}>{project.workItems.resolved}</Text>
                        <Text className={styles.statLabel}>resolved</Text>
                    </div>
                </div>

                <div className={styles.activityRow}>
                    <div className={styles.agentBadge}>
                        <BotRegular />
                        {project.agents.running > 0 ? (
                            <Badge appearance="filled" color="success">
                                {project.agents.running} agent{project.agents.running > 1 ? 's' : ''} running
                            </Badge>
                        ) : (
                            <Badge appearance="ghost" color="informative">
                                No agents active
                            </Badge>
                        )}
                    </div>
                    <div className={styles.stat}>
                        <ClockRegular className={styles.clockIcon} />
                        <Caption1>{project.lastActivity}</Caption1>
                    </div>
                </div>
            </CardPreview>
        </Card>
    )
}
