import { memo } from 'react'
import {
    makeStyles,
    mergeClasses,
    Caption1,
    Text,
} from '@fluentui/react-components'
import { usePreferences } from '../../hooks'
import type { ProjectData } from '../../models'
import { appTokens } from '../../styles/appTokens'

const useStyles = makeStyles({
    row: {
        display: 'grid',
        gridTemplateColumns: '2fr 1fr 80px 80px 80px 100px 140px',
        alignItems: 'center',
        paddingTop: appTokens.space.sm,
        paddingBottom: appTokens.space.sm,
        paddingLeft: appTokens.space.md,
        paddingRight: appTokens.space.md,
        gap: appTokens.space.md,
        borderRadius: appTokens.radius.md,
        cursor: 'pointer',
        border: appTokens.border.subtle,
        backgroundColor: appTokens.color.surface,
        ':hover': {
            backgroundColor: appTokens.color.surfaceHover,
        },
    },
    rowCompact: {
        gridTemplateColumns: '2fr 1.2fr 56px 56px 64px 72px 120px',
        paddingTop: appTokens.space.xs,
        paddingBottom: appTokens.space.xs,
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
        gap: appTokens.space.sm,
    },
    nameCell: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxxs,
        minWidth: 0,
    },
    title: {
        fontWeight: appTokens.fontWeight.semibold,
        fontSize: appTokens.fontSize.md,
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    titleCompact: {
        fontSize: appTokens.fontSize.sm,
        lineHeight: appTokens.lineHeight.snug,
    },
    description: {
        color: appTokens.color.textTertiary,
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    descriptionCompact: {
        fontSize: appTokens.fontSize.xs,
        lineHeight: appTokens.lineHeight.tight,
    },
    repoCell: {
        minWidth: 0,
        color: appTokens.color.textTertiary,
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
        textAlign: 'center',
    },
    activityCell: {
        color: appTokens.color.textMuted,
    },
})

interface ProjectRowProps {
    project: ProjectData
    onClick: () => void
}

export const ProjectRow = memo(function ProjectRow({ project, onClick }: ProjectRowProps) {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isCompact = preferences?.compactMode ?? false

    return (
        <div className={mergeClasses(styles.row, isCompact && styles.rowCompact)} onClick={onClick}>
            <div className={styles.nameCell}>
                <Text className={mergeClasses(styles.title, isCompact && styles.titleCompact)}>{project.title}</Text>
                <Caption1 className={mergeClasses(styles.description, isCompact && styles.descriptionCompact)}>
                    {project.description}
                </Caption1>
            </div>
            <div className={styles.repoCell}>
                <Caption1 className={styles.repoText}>{project.repo || 'No repo linked'}</Caption1>
            </div>
            <Text className={styles.statCell}>{project.workItems.total}</Text>
            <Text className={styles.statCell}>{project.workItems.active}</Text>
            <Text className={styles.statCell}>{project.workItems.resolved}</Text>
            <Caption1 className={styles.agentCell}>{project.agents.running}</Caption1>
            <div className={styles.activityCell}>
                <Caption1>{project.lastActivity}</Caption1>
            </div>
        </div>
    )
})
