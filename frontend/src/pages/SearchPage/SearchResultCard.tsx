import {
    makeStyles,
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
import type { ReactNode } from 'react'
import type { SearchResult } from '../../models'

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
    resultIcon: {
        fontSize: '20px',
        color: tokens.colorBrandForeground1,
        flexShrink: 0,
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
    resultDesc: {
        color: tokens.colorNeutralForeground3,
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    resultMeta: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.25rem',
        color: tokens.colorNeutralForeground4,
    },
    clockSmallIcon: {
        fontSize: '10px',
    },
})

interface SearchResultCardProps {
    result: SearchResult
}

export function SearchResultCard({ result }: SearchResultCardProps) {
    const styles = useStyles()

    return (
        <Card className={styles.resultCard}>
            <span className={styles.resultIcon}>{ICON_MAP[result.type]}</span>
            <div className={styles.resultContent}>
                <Text className={styles.resultTitle}>{result.title}</Text>
                <Caption1 className={styles.resultDesc}>{result.description}</Caption1>
                <div className={styles.resultMeta}>
                    <ClockRegular className={styles.clockSmallIcon} />
                    <Caption1>{result.meta}</Caption1>
                </div>
            </div>
            <Badge appearance="outline" color={BADGE_MAP[result.type]} size="small">
                {result.type}
            </Badge>
        </Card>
    )
}
