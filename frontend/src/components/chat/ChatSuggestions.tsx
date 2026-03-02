import { makeStyles, Button } from '@fluentui/react-components'

const useStyles = makeStyles({
    suggestionsRow: {
        display: 'flex',
        gap: '0.5rem',
        flexWrap: 'wrap',
        padding: '0 1rem 0.5rem',
    },
    suggestionChip: {
        fontSize: '11px',
    },
})

interface ChatSuggestionsProps {
    suggestions: string[]
    onSelect: (suggestion: string) => void
}

export function ChatSuggestions({ suggestions, onSelect }: ChatSuggestionsProps) {
    const styles = useStyles()

    return (
        <div className={styles.suggestionsRow}>
            {suggestions.map((suggestion) => (
                <Button
                    key={suggestion}
                    appearance="outline"
                    size="small"
                    className={styles.suggestionChip}
                    onClick={() => onSelect(suggestion)}
                >
                    {suggestion}
                </Button>
            ))}
        </div>
    )
}
