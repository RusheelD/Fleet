import { makeStyles, Button, Text, mergeClasses } from '@fluentui/react-components'
import { SparkleRegular } from '@fluentui/react-icons'
import { usePreferences } from '../../hooks/PreferencesContext'
import { appTokens } from '../../styles/appTokens'

const useStyles = makeStyles({
    suggestionsPanel: {
        alignSelf: 'center',
        width: 'min(100%, 36rem)',
        marginTop: 'auto',
        marginBottom: 'auto',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: appTokens.space.md,
        textAlign: 'center',
        paddingTop: appTokens.space.lg,
        paddingBottom: appTokens.space.lg,
        paddingLeft: appTokens.space.md,
        paddingRight: appTokens.space.md,
    },
    suggestionsPanelCompact: {
        gap: appTokens.space.sm,
        paddingTop: appTokens.space.md,
        paddingBottom: appTokens.space.md,
    },
    titleBlock: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: appTokens.space.xxs,
        maxWidth: '28rem',
    },
    title: {
        color: appTokens.color.textPrimary,
        fontWeight: appTokens.fontWeight.semibold,
    },
    subtitle: {
        color: appTokens.color.textSecondary,
        lineHeight: appTokens.lineHeight.snug,
    },
    suggestionsRow: {
        display: 'flex',
        gap: '0.5rem',
        flexWrap: 'wrap',
        justifyContent: 'center',
        width: '100%',
    },
    suggestionsRowCompact: {
        gap: '0.25rem',
    },
    suggestionChip: {
        fontSize: '11px',
        maxWidth: '100%',
    },
    suggestionChipCompact: {
        fontSize: '10px',
        lineHeight: '14px',
    },
})

interface ChatSuggestionsProps {
    suggestions: string[]
    onSelect: (suggestion: string) => void
    title?: string
    subtitle?: string
}

export function ChatSuggestions({
    suggestions,
    onSelect,
    title = 'What should Fleet work on?',
    subtitle,
}: ChatSuggestionsProps) {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isCompact = preferences?.compactMode ?? false

    return (
        <div className={mergeClasses(styles.suggestionsPanel, isCompact && styles.suggestionsPanelCompact)}>
            <div className={styles.titleBlock}>
                <Text size={400} className={styles.title}>{title}</Text>
                {subtitle && <Text size={200} className={styles.subtitle}>{subtitle}</Text>}
            </div>
            <div className={mergeClasses(styles.suggestionsRow, isCompact && styles.suggestionsRowCompact)}>
                {suggestions.map((suggestion) => (
                    <Button
                        key={suggestion}
                        appearance="outline"
                        size="small"
                        icon={<SparkleRegular />}
                        className={mergeClasses(styles.suggestionChip, isCompact && styles.suggestionChipCompact)}
                        onClick={() => onSelect(suggestion)}
                    >
                        {suggestion}
                    </Button>
                ))}
            </div>
        </div>
    )
}
