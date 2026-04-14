import { memo } from 'react'
import {
    makeStyles,
    mergeClasses,
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
import { useIsMobile } from '../../hooks'
import { appTokens } from '../../styles/appTokens'
import type { ProjectData } from '../../models'
import { InfoBadge } from '../../components/shared/InfoBadge'

const useStyles = makeStyles({
    projectCard: {
        cursor: 'pointer',
        transitionProperty: 'transform, box-shadow',
        transitionDuration: appTokens.motion.fast,
        minWidth: 0,
        overflow: 'hidden',
        border: appTokens.border.subtle,
        backgroundImage: `linear-gradient(155deg, ${appTokens.color.surface} 0%, ${appTokens.color.surfaceAlt} 100%)`,
        ':hover': {
            boxShadow: appTokens.shadow.cardHover,
            transform: 'translateY(-2px)',
        },
    },
    projectCardMobile: {
        borderRadius: appTokens.radius.lg,
    },
    title: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    description: {
        color: appTokens.color.textTertiary,
        display: '-webkit-box',
        WebkitLineClamp: '2',
        WebkitBoxOrient: 'vertical',
        overflow: 'hidden',
    },
    cardPreview: {
        padding: '0 1rem 1rem',
        display: 'flex',
        flexDirection: 'column',
        gap: '0.875rem',
        minWidth: 0,
    },
    cardPreviewMobile: {
        paddingTop: '0.125rem',
        paddingBottom: '0.875rem',
        paddingLeft: '0.875rem',
        paddingRight: '0.875rem',
        gap: '0.75rem',
    },
    repoRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
        color: appTokens.color.textTertiary,
        minWidth: 0,
        flexWrap: 'wrap',
    },
    repoText: {
        minWidth: 0,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    statsRow: {
        display: 'grid',
        gridTemplateColumns: 'repeat(3, minmax(0, 1fr))',
        gap: '0.5rem',
    },
    stat: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0.25rem',
        padding: appTokens.space.sm,
        borderRadius: appTokens.radius.md,
        backgroundColor: appTokens.color.pageBackground,
        border: appTokens.border.subtle,
    },
    statValue: {
        fontWeight: 700,
        fontSize: '18px',
    },
    statLabel: {
        fontSize: '12px',
        color: appTokens.color.textMuted,
    },
    activityRow: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: '0.5rem',
        flexWrap: 'wrap',
        minWidth: 0,
    },
    activityRowMobile: {
        flexDirection: 'column',
        alignItems: 'flex-start',
    },
    agentBadge: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
        minWidth: 0,
        flexWrap: 'wrap',
    },
    activityTime: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.25rem',
    },
    clockIcon: {
        fontSize: '12px',
        color: appTokens.color.textMuted,
    },
    topStripe: {
        height: '4px',
        width: '100%',
        backgroundImage: `linear-gradient(90deg, ${appTokens.color.brand} 0%, ${appTokens.color.info} 100%)`,
    },
    repoBadge: {
        width: 'fit-content',
    },
})

interface ProjectCardProps {
    project: ProjectData
    onClick: () => void
}

export const ProjectCard = memo(function ProjectCard({ project, onClick }: ProjectCardProps) {
    const styles = useStyles()
    const isMobile = useIsMobile()

    return (
        <Card className={mergeClasses(styles.projectCard, isMobile && styles.projectCardMobile)} onClick={onClick}>
            <div className={styles.topStripe} />
            <CardHeader
                header={<Title3 className={styles.title}>{project.title}</Title3>}
                description={<Caption1 className={styles.description}>{project.description}</Caption1>}
            />
            <CardPreview className={mergeClasses(styles.cardPreview, isMobile && styles.cardPreviewMobile)}>
                <div className={styles.repoRow}>
                    <FolderRegular />
                    <Text size={200} className={styles.repoText}>{project.repo || 'Repository not linked yet'}</Text>
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

                <div className={mergeClasses(styles.activityRow, isMobile && styles.activityRowMobile)}>
                    <div className={styles.agentBadge}>
                        <BotRegular />
                        {project.agents.running > 0 ? (
                            <Badge appearance="filled" color="success">
                                {project.agents.running} agent{project.agents.running > 1 ? 's' : ''} running
                            </Badge>
                        ) : (
                            <InfoBadge appearance="ghost">
                                No agents active
                            </InfoBadge>
                        )}
                    </div>
                    <div className={styles.activityTime}>
                        <ClockRegular className={styles.clockIcon} />
                        <Caption1>{project.lastActivity}</Caption1>
                    </div>
                </div>
                <InfoBadge appearance="tint" className={styles.repoBadge}>
                    {project.repo ? 'Repo linked' : 'Needs repo'}
                </InfoBadge>
            </CardPreview>
        </Card>
    )
})
