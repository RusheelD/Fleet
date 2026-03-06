import {
    makeStyles,
    mergeClasses,
    tokens,
    Caption1,
    Text,
    Card,
    Badge,
} from '@fluentui/react-components'
import {
    FolderRegular,
    BoardRegular,
    ChatRegular,
    BotRegular,
    ClockRegular,
} from '@fluentui/react-icons'
import { useNavigate } from 'react-router-dom'
import type { ReactNode } from 'react'
import type { SearchResult } from '../../models'
import { usePreferences } from '../../hooks'

const ICON_MAP: Record<string, ReactNode> = {
    project: <FolderRegular />,
    workitem: <BoardRegular />,
    chat: <ChatRegular />,
    agent: <BotRegular />,
}

const BADGE_MAP: Record<string, 'brand' | 'success' | 'warning' | 'informative'> = {
    project: 'brand',
    workitem: 'informative',
    chat: 'success',
    agent: 'warning',
}

const useStyles = makeStyles({
    resultCard: {
        padding: '0.75rem 1rem',
        display: 'flex',
        alignItems: 'center',
        gap: '0.75rem',
        cursor: 'pointer',
        ':hover': {
            boxShadow: tokens.shadow4,
        },
    },
    resultCardCompact: {
        paddingTop: '0.375rem',
        paddingBottom: '0.375rem',
        paddingLeft: '0.5rem',
        paddingRight: '0.5rem',
        gap: '0.5rem',
        borderRadius: tokens.borderRadiusMedium,
    },
    resultIcon: {
        fontSize: '20px',
        color: tokens.colorBrandForeground1,
        flexShrink: 0,
    },
    resultIconCompact: {
        fontSize: '14px',
    },
    resultContent: {
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        gap: '0.125rem',
        minWidth: 0,
    },
    resultTitle: {
        fontWeight: 600,
        fontSize: '14px',
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    resultTitleCompact: {
        fontSize: '12px',
        lineHeight: '16px',
    },
    resultDesc: {
        color: tokens.colorNeutralForeground3,
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    resultDescCompact: {
        fontSize: '11px',
        lineHeight: '14px',
    },
    resultMeta: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.25rem',
        color: tokens.colorNeutralForeground4,
    },
    resultMetaCompact: {
        gap: '2px',
    },
    clockSmallIcon: {
        fontSize: '10px',
    },
    clockSmallIconCompact: {
        fontSize: '9px',
    },
})

interface SearchResultCardProps {
    result: SearchResult
}

export function SearchResultCard({ result }: SearchResultCardProps) {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isCompact = preferences?.compactMode ?? false
    const navigate = useNavigate()

    const handleClick = () => {
        const slug = result.projectSlug
        if (!slug) return

        switch (result.type) {
            case 'project':
                navigate(`/projects/${slug}`)
                break
            case 'workitem':
                navigate(`/projects/${slug}/work-items`)
                break
            case 'agent':
                navigate(`/projects/${slug}/agents`)
                break
            case 'chat':
                navigate(`/projects/${slug}`, { state: { openChat: true } })
                break
        }
    }

    return (
        <Card className={mergeClasses(styles.resultCard, isCompact && styles.resultCardCompact)} onClick={handleClick}>
            <span className={mergeClasses(styles.resultIcon, isCompact && styles.resultIconCompact)}>{ICON_MAP[result.type]}</span>
            <div className={styles.resultContent}>
                <Text className={mergeClasses(styles.resultTitle, isCompact && styles.resultTitleCompact)}>{result.title}</Text>
                <Caption1 className={mergeClasses(styles.resultDesc, isCompact && styles.resultDescCompact)}>{result.description}</Caption1>
                <div className={mergeClasses(styles.resultMeta, isCompact && styles.resultMetaCompact)}>
                    <ClockRegular className={mergeClasses(styles.clockSmallIcon, isCompact && styles.clockSmallIconCompact)} />
                    <Caption1>{result.meta}</Caption1>
                </div>
            </div>
            <Badge appearance="outline" color={BADGE_MAP[result.type]} size="small">
                {result.type}
            </Badge>
        </Card>
    )
}
