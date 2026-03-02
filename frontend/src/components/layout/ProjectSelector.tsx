import {
    makeStyles,
    tokens,
    Text,
    Tooltip,
} from '@fluentui/react-components'
import {
    FolderOpenRegular,
    ChevronDownRegular,
} from '@fluentui/react-icons'
import { useNavigate } from 'react-router-dom'

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
})

interface ProjectSelectorProps {
    projectName: string
    expanded: boolean
}

export function ProjectSelector({ projectName, expanded }: ProjectSelectorProps) {
    const styles = useStyles()
    const navigate = useNavigate()

    if (expanded) {
        return (
            <div
                className={styles.projectSelector}
                onClick={() => navigate('/projects')}
                role="button"
                tabIndex={0}
                onKeyDown={(e) => e.key === 'Enter' && navigate('/projects')}
            >
                <FolderOpenRegular className={styles.projectSelectorIcon} />
                <div className={styles.projectSelectorInfo}>
                    <Text className={styles.projectSelectorLabel}>Project</Text>
                    <Text className={styles.projectSelectorName}>{projectName}</Text>
                </div>
                <ChevronDownRegular className={styles.projectSelectorChevron} />
            </div>
        )
    }

    return (
        <Tooltip content={`${projectName} — Switch project`} relationship="label" positioning="after">
            <button
                className={styles.collapsedButton}
                onClick={() => navigate('/projects')}
            >
                <span className={styles.collapsedIcon}><FolderOpenRegular /></span>
            </button>
        </Tooltip>
    )
}
