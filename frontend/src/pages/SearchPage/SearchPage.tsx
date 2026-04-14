import { useState } from 'react'
import {
    Button,
    Card,
    Caption1,
    makeStyles,
    mergeClasses,
    Input,
    Spinner,
    Tab,
    TabList,
} from '@fluentui/react-components'
import {
    SearchRegular,
    FolderRegular,
    BoardRegular,
    ChatRegular,
    BotRegular,
} from '@fluentui/react-icons'
import { EmptyState, PageShell } from '../../components/shared'
import { SearchResultCard } from './'
import { useSearch } from '../../proxies'
import { getSearchTypeForCategory, type SearchCategory } from './searchCategory'
import { usePreferences } from '../../hooks'
import { appTokens } from '../../styles/appTokens'

const useStyles = makeStyles({
    searchPanel: {
        padding: appTokens.space.lg,
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.md,
    },
    searchPanelCompact: {
        paddingTop: appTokens.space.md,
        paddingBottom: appTokens.space.md,
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
    },
    fullWidth: {
        width: '100%',
    },
    resultsList: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.sm,
    },
    resultsListCompact: {
        gap: appTokens.space.xs,
    },
    tabList: {
        overflowX: 'auto',
        paddingBottom: appTokens.space.xxs,
    },
    resultsHeader: {
        color: appTokens.color.textTertiary,
    },
})

export function SearchPage() {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isCompact = preferences?.compactMode ?? false
    const [query, setQuery] = useState('')
    const [category, setCategory] = useState<SearchCategory>('all')
    const { data: results, isLoading } = useSearch(query, getSearchTypeForCategory(category))
    const filtered = results ?? []
    const hasQuery = query.trim().length > 0

    return (
        <PageShell
            title="Search"
            subtitle="Find projects, work items, chats, and agent activity from one focused place."
            maxWidth="medium"
        >
            <Card className={mergeClasses(styles.searchPanel, isCompact && styles.searchPanelCompact)}>
                <Caption1>Search across your workspace without bouncing between pages.</Caption1>
                <Input
                    className={styles.fullWidth}
                    contentBefore={<SearchRegular />}
                    placeholder="Search everything..."
                    size={isCompact ? 'medium' : 'large'}
                    value={query}
                    onChange={(_e, data) => setQuery(data.value)}
                />
                <TabList
                    selectedValue={category}
                    onTabSelect={(_e, data) => setCategory(data.value as SearchCategory)}
                    size={isCompact ? 'small' : 'medium'}
                    className={styles.tabList}
                >
                    <Tab value="all">All ({filtered.length})</Tab>
                    <Tab value="projects" icon={<FolderRegular />}>Projects</Tab>
                    <Tab value="workitems" icon={<BoardRegular />}>Work Items</Tab>
                    <Tab value="chats" icon={<ChatRegular />}>Chats</Tab>
                    <Tab value="agents" icon={<BotRegular />}>Agents</Tab>
                </TabList>
            </Card>

            {!hasQuery ? (
                <EmptyState
                    icon={<SearchRegular style={{ fontSize: '48px' }} />}
                    title="Start with a project, work item, chat, or agent"
                    description="Enter a search term above and Fleet will narrow results across the parts of the workspace that matter."
                />
            ) : isLoading ? (
                <Spinner label="Searching..." />
            ) : filtered.length > 0 ? (
                <>
                    <Caption1 className={styles.resultsHeader}>
                        {filtered.length} result{filtered.length === 1 ? '' : 's'} for "{query}"
                    </Caption1>
                    <div className={mergeClasses(styles.resultsList, isCompact && styles.resultsListCompact)}>
                        {filtered.map((result, i) => (
                            <SearchResultCard key={i} result={result} />
                        ))}
                    </div>
                </>
            ) : (
                <EmptyState
                    icon={<SearchRegular style={{ fontSize: '48px' }} />}
                    title={`No matches for "${query}"`}
                    description="Try a different keyword, switch categories, or broaden the search a bit."
                    actions={(
                        <Button appearance="secondary" onClick={() => setQuery('')}>
                            Clear Search
                        </Button>
                    )}
                />
            )}
        </PageShell>
    )
}
