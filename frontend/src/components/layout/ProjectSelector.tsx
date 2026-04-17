import { useState, useCallback } from 'react'
import {
    makeStyles,
    Text,
    Tooltip,
    Popover,
    PopoverTrigger,
    PopoverSurface,
    mergeClasses,
} from '@fluentui/react-components'
import {
    FolderOpenRegular,
    ChevronDownRegular,
    ChevronUpRegular,
    CheckmarkRegular,
} from '@fluentui/react-icons'
import { useNavigate, useParams } from 'react-router-dom'
import { useProjects } from '../../proxies/dataClient'
import { appTokens } from '../../styles/appTokens'

const useStyles = makeStyles({
    projectSelector: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.sm,
        paddingTop: appTokens.space.sm,
        paddingBottom: appTokens.space.sm,
        paddingLeft: appTokens.space.md,
        paddingRight: appTokens.space.md,
        marginTop: appTokens.space.xs,
        marginRight: appTokens.space.xs,
        marginLeft: appTokens.space.xs,
        borderRadius: appTokens.radius.md,
        cursor: 'pointer',
        backgroundColor: appTokens.color.surfaceAlt,
        border: appTokens.border.subtle,
        width: 'calc(100% - 1rem)',
        textAlign: 'left',
        ':hover': {
            backgroundColor: appTokens.color.surfaceAltHover,
        },
        ':focus-visible': {
            outline: `2px solid ${appTokens.color.focusOutline}`,
            outlineOffset: '-2px',
        },
    },
    projectSelectorIcon: {
        fontSize: appTokens.fontSize.iconMd,
        color: appTokens.color.brand,
        flexShrink: 0,
    },
    projectSelectorInfo: {
        display: 'flex',
        flexDirection: 'column',
        flex: 1,
        minWidth: 0,
    },
    projectSelectorLabel: {
        fontSize: appTokens.fontSize.xxs,
        fontWeight: appTokens.fontWeight.semibold,
        textTransform: 'uppercase',
        letterSpacing: '0.05em',
        color: appTokens.color.textMuted,
        lineHeight: 1,
    },
    projectSelectorName: {
        fontSize: appTokens.fontSize.md,
        fontWeight: appTokens.fontWeight.semibold,
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    projectSelectorChevron: {
        fontSize: appTokens.fontSize.iconXs,
        color: appTokens.color.textMuted,
        flexShrink: 0,
        transition: `transform ${appTokens.motion.fast} ease`,
    },
    /* collapsed variant re-uses navItem-like button */
    collapsedButton: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        gap: appTokens.space.md,
        padding: `0 ${appTokens.space.md}`,
        borderRadius: appTokens.radius.md,
        cursor: 'pointer',
        color: appTokens.color.textSecondary,
        transition: `background ${appTokens.motion.fast}`,
        borderTopStyle: 'none',
        borderRightStyle: 'none',
        borderBottomStyle: 'none',
        borderLeftStyle: 'none',
        backgroundColor: 'transparent',
        width: '100%',
        textAlign: 'left',
        fontSize: appTokens.fontSize.md,
        minHeight: '34px',
        position: 'relative',
        ':hover': {
            backgroundColor: appTokens.color.surfaceHover,
            color: appTokens.color.textPrimary,
        },
        ':focus-visible': {
            outline: `2px solid ${appTokens.color.focusOutline}`,
            outlineOffset: '-2px',
        },
    },
    collapsedIcon: {
        fontSize: appTokens.fontSize.iconMd,
        flexShrink: 0,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        width: '20px',
    },
    /* Dropdown styles */
    dropdownSurface: {
        padding: appTokens.space.xxs,
        minWidth: '220px',
        maxWidth: '300px',
        maxHeight: '320px',
        overflowY: 'auto',
    },
    dropdownHeader: {
        display: 'flex',
        alignItems: 'flex-start',
        justifyContent: 'space-between',
        gap: appTokens.space.md,
        paddingTop: appTokens.space.xs,
        paddingRight: appTokens.space.sm,
        paddingBottom: appTokens.space.xxs,
        paddingLeft: appTokens.space.sm,
    },
    dropdownHeaderInfo: {
        display: 'flex',
        flexDirection: 'column',
        gap: '2px',
        minWidth: 0,
        flex: 1,
    },
    dropdownTitle: {
        display: 'block',
        fontSize: appTokens.fontSize.xs,
        fontWeight: appTokens.fontWeight.semibold,
        textTransform: 'uppercase',
        letterSpacing: '0.05em',
        color: appTokens.color.textMuted,
        lineHeight: 1.2,
    },
    dropdownMeta: {
        display: 'block',
        fontSize: appTokens.fontSize.xs,
        color: appTokens.color.textMuted,
        lineHeight: 1.3,
    },
    allProjectsLink: {
        flexShrink: 0,
        display: 'inline-flex',
        alignItems: 'center',
        fontSize: appTokens.fontSize.xs,
        color: appTokens.color.brand,
        cursor: 'pointer',
        whiteSpace: 'nowrap',
        ':hover': {
            textDecorationLine: 'underline',
        },
    },
    projectItem: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.sm,
        paddingTop: appTokens.space.sm,
        paddingBottom: appTokens.space.sm,
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
        borderRadius: appTokens.radius.md,
        cursor: 'pointer',
        ':hover': {
            backgroundColor: appTokens.color.surfaceHover,
        },
    },
    projectItemActive: {
        backgroundColor: appTokens.color.surfaceSelected,
    },
    projectItemIcon: {
        fontSize: appTokens.fontSize.iconSm,
        color: appTokens.color.brand,
        flexShrink: 0,
    },
    projectItemInfo: {
        display: 'flex',
        flexDirection: 'column',
        flex: 1,
        minWidth: 0,
    },
    projectItemName: {
        fontSize: appTokens.fontSize.md,
        fontWeight: appTokens.fontWeight.semibold,
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    projectItemMeta: {
        fontSize: appTokens.fontSize.xs,
        color: appTokens.color.textMuted,
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    checkIcon: {
        fontSize: appTokens.fontSize.sm,
        color: appTokens.color.brand,
        flexShrink: 0,
    },
    checkPlaceholder: {
        width: appTokens.fontSize.sm,
        flexShrink: 0,
    },
    emptyProjects: {
        paddingTop: appTokens.space.md,
        paddingBottom: appTokens.space.md,
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
        color: appTokens.color.textMuted,
    },
})

interface ProjectSelectorProps {
    projectName: string
    expanded: boolean
}

export function ProjectSelector({ projectName, expanded }: ProjectSelectorProps) {
    const styles = useStyles()
    const navigate = useNavigate()
    const { slug: currentSlug } = useParams()
    const { data: projects } = useProjects()
    const [open, setOpen] = useState(false)

    const handleProjectSwitch = useCallback(
        (projectSlug: string) => {
            setOpen(false)
            if (projectSlug !== currentSlug) {
                navigate(`/projects/${projectSlug}`)
            }
        },
        [currentSlug, navigate],
    )

    const dropdown = (
        <PopoverSurface className={styles.dropdownSurface}>
            <div className={styles.dropdownHeader}>
                <div className={styles.dropdownHeaderInfo}>
                    <Text className={styles.dropdownTitle}>Switch Project</Text>
                    <Text className={styles.dropdownMeta}>
                        {projects?.length ?? 0} project{projects?.length === 1 ? '' : 's'}
                    </Text>
                </div>
                <Text
                    className={styles.allProjectsLink}
                    onClick={() => {
                        setOpen(false)
                        navigate('/projects')
                    }}
                >
                    View all
                </Text>
            </div>
            {projects?.length ? projects.map((project) => {
                const isActive = project.slug === currentSlug
                return (
                    <div
                        key={project.id}
                        className={mergeClasses(
                            styles.projectItem,
                            isActive && styles.projectItemActive,
                        )}
                        role="button"
                        tabIndex={0}
                        onClick={() => handleProjectSwitch(project.slug)}
                        onKeyDown={(e) =>
                            e.key === 'Enter' && handleProjectSwitch(project.slug)
                        }
                    >
                        <FolderOpenRegular className={styles.projectItemIcon} />
                        <div className={styles.projectItemInfo}>
                            <Text className={styles.projectItemName}>
                                {project.title}
                            </Text>
                            <Text className={styles.projectItemMeta}>
                                {project.workItems.total} items | {project.agents.running} agents active
                            </Text>
                        </div>
                        {isActive ? (
                            <CheckmarkRegular className={styles.checkIcon} />
                        ) : (
                            <span className={styles.checkPlaceholder} />
                        )}
                    </div>
                )
            }) : (
                <Text className={styles.emptyProjects}>No projects available yet.</Text>
            )}
        </PopoverSurface>
    )

    if (expanded) {
        return (
            <Popover
                open={open}
                onOpenChange={(_e, data) => setOpen(data.open)}
                positioning="below-start"
                trapFocus
            >
                <PopoverTrigger disableButtonEnhancement>
                    <button
                        type="button"
                        className={styles.projectSelector}
                    >
                        <FolderOpenRegular className={styles.projectSelectorIcon} />
                        <div className={styles.projectSelectorInfo}>
                            <Text className={styles.projectSelectorLabel}>Project</Text>
                            <Text className={styles.projectSelectorName}>{projectName}</Text>
                        </div>
                        {open ? (
                            <ChevronUpRegular className={styles.projectSelectorChevron} />
                        ) : (
                            <ChevronDownRegular className={styles.projectSelectorChevron} />
                        )}
                    </button>
                </PopoverTrigger>
                {dropdown}
            </Popover>
        )
    }

    return (
        <Popover
            open={open}
            onOpenChange={(_e, data) => setOpen(data.open)}
            positioning="after"
            trapFocus
        >
            <PopoverTrigger disableButtonEnhancement>
                <Tooltip content={`${projectName} - Switch project`} relationship="label" positioning="after">
                    <button className={styles.collapsedButton} type="button" aria-label={`${projectName} - Switch project`}>
                        <span className={styles.collapsedIcon}><FolderOpenRegular /></span>
                    </button>
                </Tooltip>
            </PopoverTrigger>
            {dropdown}
        </Popover>
    )
}
