import { makeStyles, Button, mergeClasses } from '@fluentui/react-components'
import { usePreferences } from '../../hooks'

const useStyles = makeStyles({
    suggestionsRow: {
        display: 'flex',
        gap: '0.5rem',
        flexWrap: 'wrap',
        padding: '0 1rem 0.5rem',
    },
    suggestionsRowCompact: {
        gap: '0.25rem',
        paddingTop: 0,
        paddingBottom: '0.25rem',
        paddingLeft: '0.5rem',
        paddingRight: '0.5rem',
    },
    suggestionChip: {
        fontSize: '11px',
    },
    suggestionChipCompact: {
        fontSize: '10px',
        lineHeight: '14px',
    },
})

interface ChatSuggestionsProps {
    suggestions: string[]
    onSelect: (suggestion: string) => void
}

export function ChatSuggestions({ suggestions, onSelect }: ChatSuggestionsProps) {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isCompact = preferences?.compactMode ?? false

    return (
        <div className={mergeClasses(styles.suggestionsRow, isCompact && styles.suggestionsRowCompact)}>
            {suggestions.map((suggestion) => (
                <Button
                    key={suggestion}
                    appearance="outline"
                    size="small"
                    className={mergeClasses(styles.suggestionChip, isCompact && styles.suggestionChipCompact)}
                    onClick={() => onSelect(suggestion)}
                >
                    {suggestion}
                </Button>
            ))}
        </div>
    )
}
