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

type SearchCategory = 'all' | 'projects' | 'workitems' | 'chats' | 'agents'

const categoryToType: Record<SearchCategory, string | undefined> = {
    all: undefined,
    projects: 'project',
    workitems: 'workitem',
    chats: 'chat',
    agents: 'agent',
}

const useStyles = makeStyles({
    page: {
        padding: '1.5rem 2rem',
        maxWidth: '900px',
        margin: '0 auto',
        width: '100%',
    },
    searchBox: {
        marginBottom: '1rem',
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
})

export function SearchPage() {
    const styles = useStyles()
    const [query, setQuery] = useState('')
    const [category, setCategory] = useState<SearchCategory>('all')
    const { data: results, isLoading } = useSearch(query, categoryToType[category])

    const filtered = results ?? []

    return (
        <div className={styles.page}>
            <PageHeader
                title="Search"
                subtitle="Search across projects, work items, chats, and agents"
            />

            <Input
                className={mergeClasses(styles.searchBox, styles.fullWidth)}
                contentBefore={<SearchRegular />}
                placeholder="Search everything..."
                size="large"
                value={query}
                onChange={(_e, data) => setQuery(data.value)}
            />

            <TabList selectedValue={category} onTabSelect={(_e, data) => setCategory(data.value as SearchCategory)}>
                <Tab value="all">All ({filtered.length})</Tab>
                <Tab value="projects" icon={<FolderRegular />}>Projects</Tab>
                <Tab value="workitems" icon={<BoardRegular />}>Work Items</Tab>
                <Tab value="chats" icon={<ChatRegular />}>Chats</Tab>
                <Tab value="agents" icon={<BotRegular />}>Agents</Tab>
            </TabList>

            {isLoading ? (
                <Spinner label="Searching..." />
            ) : filtered.length > 0 ? (
                <div className={styles.resultsList}>
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
