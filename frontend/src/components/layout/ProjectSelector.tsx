import { useState, useCallback } from 'react'
import {
    makeStyles,
    tokens,
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
import { useProjects } from '../../proxies'

const useStyles = makeStyles({
    projectSelector: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.625rem',
        padding: '0.625rem 0.75rem',
        margin: '0.375rem 0.375rem 0',
        borderRadius: tokens.borderRadiusMedium,
        cursor: 'pointer',
        backgroundColor: tokens.colorNeutralBackground3,
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground3Hover,
        },
    },
    projectSelectorIcon: {
        fontSize: '18px',
        color: tokens.colorBrandForeground1,
        flexShrink: 0,
    },
    projectSelectorInfo: {
        display: 'flex',
        flexDirection: 'column',
        flex: 1,
        minWidth: 0,
    },
    projectSelectorLabel: {
        fontSize: '10px',
        fontWeight: 600,
        textTransform: 'uppercase',
        letterSpacing: '0.05em',
        color: tokens.colorNeutralForeground4,
        lineHeight: 1,
    },
    projectSelectorName: {
        fontSize: '13px',
        fontWeight: 600,
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    projectSelectorChevron: {
        fontSize: '12px',
        color: tokens.colorNeutralForeground4,
        flexShrink: 0,
        transition: 'transform 0.15s ease',
    },
    /* collapsed variant re-uses navItem-like button */
    collapsedButton: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.75rem',
        padding: '0 0.75rem',
        borderRadius: tokens.borderRadiusMedium,
        cursor: 'pointer',
        color: tokens.colorNeutralForeground2,
        transition: 'background 0.1s',
        border: 'none',
        backgroundColor: 'transparent',
        width: '100%',
        textAlign: 'left',
        fontSize: '13px',
        minHeight: '34px',
        position: 'relative',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
            color: tokens.colorNeutralForeground1,
        },
    },
    collapsedIcon: {
        fontSize: '18px',
        flexShrink: 0,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        width: '20px',
    },
    /* Dropdown styles */
    dropdownSurface: {
        padding: '0.25rem',
        minWidth: '220px',
        maxWidth: '300px',
        maxHeight: '320px',
        overflowY: 'auto',
    },
    dropdownHeader: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '0.375rem 0.5rem 0.25rem',
    },
    dropdownTitle: {
        fontSize: '11px',
        fontWeight: 600,
        textTransform: 'uppercase',
        letterSpacing: '0.05em',
        color: tokens.colorNeutralForeground4,
    },
    allProjectsLink: {
        fontSize: '11px',
        color: tokens.colorBrandForeground1,
        cursor: 'pointer',
        ':hover': {
            textDecorationLine: 'underline',
        },
    },
    projectItem: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
        padding: '0.5rem 0.5rem',
        borderRadius: tokens.borderRadiusMedium,
        cursor: 'pointer',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },
    projectItemActive: {
        backgroundColor: tokens.colorNeutralBackground1Selected,
    },
    projectItemIcon: {
        fontSize: '16px',
        color: tokens.colorBrandForeground1,
        flexShrink: 0,
    },
    projectItemInfo: {
        display: 'flex',
        flexDirection: 'column',
        flex: 1,
        minWidth: 0,
    },
    projectItemName: {
        fontSize: '13px',
        fontWeight: 600,
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    projectItemMeta: {
        fontSize: '11px',
        color: tokens.colorNeutralForeground4,
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    checkIcon: {
        fontSize: '14px',
        color: tokens.colorBrandForeground1,
        flexShrink: 0,
    },
    checkPlaceholder: {
        width: '14px',
        flexShrink: 0,
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
                <Text className={styles.dropdownTitle}>Switch Project</Text>
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
            {projects?.map((project) => {
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
                                {project.workItems.total} items · {project.agents.running} agents active
                            </Text>
                        </div>
                        {isActive ? (
                            <CheckmarkRegular className={styles.checkIcon} />
                        ) : (
                            <span className={styles.checkPlaceholder} />
                        )}
                    </div>
                )
            })}
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
                    <div
                        className={styles.projectSelector}
                        role="button"
                        tabIndex={0}
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
                    </div>
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
                <Tooltip content={`${projectName} — Switch project`} relationship="label" positioning="after">
                    <button className={styles.collapsedButton}>
                        <span className={styles.collapsedIcon}><FolderOpenRegular /></span>
                    </button>
                </Tooltip>
            </PopoverTrigger>
            {dropdown}
        </Popover>
    )
}
