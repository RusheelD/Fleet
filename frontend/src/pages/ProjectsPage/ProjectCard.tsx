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
} from '@fluentui/react-components'
import {
    FolderRegular,
    ClockRegular,
    BotRegular,
} from '@fluentui/react-icons'
import { useIsMobile } from '../../hooks'
import { appTokens } from '../../styles/appTokens'
import type { ProjectData } from '../../models'

const useStyles = makeStyles({
    projectCard: {
        cursor: 'pointer',
        transitionProperty: 'transform, box-shadow',
        transitionDuration: appTokens.motion.fast,
        minWidth: 0,
        overflow: 'hidden',
        border: appTokens.border.subtle,
        backgroundColor: appTokens.color.surface,
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
    statsText: {
        color: appTokens.color.textSecondary,
        overflowWrap: 'anywhere',
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
            <CardHeader
                header={<Title3 className={styles.title}>{project.title}</Title3>}
                description={<Caption1 className={styles.description}>{project.description}</Caption1>}
            />
            <CardPreview className={mergeClasses(styles.cardPreview, isMobile && styles.cardPreviewMobile)}>
                <div className={styles.repoRow}>
                    <FolderRegular />
                    <Text size={200} className={styles.repoText}>{project.repo || 'Repository not linked yet'}</Text>
                </div>

                <Caption1 className={styles.statsText}>
                    {project.workItems.total} items | {project.workItems.active} active | {project.workItems.resolved} resolved
                </Caption1>

                <div className={mergeClasses(styles.activityRow, isMobile && styles.activityRowMobile)}>
                    <div className={styles.agentBadge}>
                        <BotRegular />
                        <Caption1>
                            {project.agents.running > 0
                                ? `${project.agents.running} agent${project.agents.running > 1 ? 's' : ''} running`
                                : 'No agents active'}
                        </Caption1>
                    </div>
                    <div className={styles.activityTime}>
                        <ClockRegular className={styles.clockIcon} />
                        <Caption1>{project.lastActivity}</Caption1>
                    </div>
                </div>
            </CardPreview>
        </Card>
    )
})
