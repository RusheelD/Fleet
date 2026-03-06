import { useState } from 'react'
import {
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
import { PageHeader, EmptyState } from '../../components/shared'
import { SearchResultCard } from './'
import { useSearch } from '../../proxies'
import { getSearchTypeForCategory, type SearchCategory } from './searchCategory'
import { usePreferences } from '../../hooks'

const useStyles = makeStyles({
    page: {
        padding: '1.5rem 2rem',
        maxWidth: '900px',
        margin: '0 auto',
        width: '100%',
    },
    pageCompact: {
        paddingTop: '1rem',
        paddingBottom: '1rem',
        paddingLeft: '1rem',
        paddingRight: '1rem',
        maxWidth: '760px',
    },
    searchBox: {
        marginBottom: '1rem',
    },
    searchBoxCompact: {
        marginBottom: '0.5rem',
    },
    fullWidth: {
        width: '100%',
    },
    resultsList: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0.5rem',
        marginTop: '1rem',
    },
    resultsListCompact: {
        gap: '0.375rem',
        marginTop: '0.5rem',
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

    return (
        <div className={mergeClasses(styles.page, isCompact && styles.pageCompact)}>
            <PageHeader
                title="Search"
                subtitle="Search across projects, work items, chats, and agents"
            />

            <Input
                className={mergeClasses(styles.searchBox, isCompact && styles.searchBoxCompact, styles.fullWidth)}
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
            >
                <Tab value="all">All ({filtered.length})</Tab>
                <Tab value="projects" icon={<FolderRegular />}>Projects</Tab>
                <Tab value="workitems" icon={<BoardRegular />}>Work Items</Tab>
                <Tab value="chats" icon={<ChatRegular />}>Chats</Tab>
                <Tab value="agents" icon={<BotRegular />}>Agents</Tab>
            </TabList>

            {isLoading ? (
                <Spinner label="Searching..." />
            ) : filtered.length > 0 ? (
                <div className={mergeClasses(styles.resultsList, isCompact && styles.resultsListCompact)}>
                    {filtered.map((result, i) => (
                        <SearchResultCard key={i} result={result} />
                    ))}
                </div>
            ) : (
                <EmptyState
                    icon={<SearchRegular style={{ fontSize: '48px' }} />}
                    title="No results found"
                    description="Try a different search term or category"
                />
            )}
        </div>
    )
}
