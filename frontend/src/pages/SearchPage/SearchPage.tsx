import { useState } from 'react'
import {
    makeStyles,
    mergeClasses,
    Input,
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
import { SearchResultCard } from './SearchResultCard'
import type { SearchResult } from '../../models'

type SearchCategory = 'all' | 'projects' | 'workitems' | 'chats' | 'agents'

const MOCK_RESULTS: SearchResult[] = [
    { type: 'project', title: 'Fleet Platform', description: 'AI-powered project management', meta: 'Last active 2 hours ago' },
    { type: 'workitem', title: '#101 — Set up authentication with OAuth', description: 'In Progress (AI) · Priority 1', meta: 'Fleet Platform' },
    { type: 'workitem', title: '#104 — Implement work item board view', description: 'In Progress (AI) · Priority 2', meta: 'Fleet Platform' },
    { type: 'chat', title: 'Product Spec Discussion', description: '12 items generated from conversation', meta: 'Fleet Platform · 2 hours ago' },
    { type: 'agent', title: 'Backend Agent — OAuth Implementation', description: 'Running · 45% complete', meta: 'Fleet Platform · Work Item #101' },
    { type: 'workitem', title: '#105 — Set up CI/CD pipeline', description: 'Resolved (AI) · Priority 1', meta: 'Fleet Platform' },
    { type: 'project', title: 'E-Commerce API', description: 'RESTful API for online marketplace', meta: 'Last active 5 hours ago' },
    { type: 'chat', title: 'Auth Implementation Plan', description: 'OAuth flow discussion and planning', meta: 'Fleet Platform · 1 day ago' },
]

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

    const filtered = MOCK_RESULTS.filter((r) => {
        if (category !== 'all') {
            const typeMap: Record<string, string> = { projects: 'project', workitems: 'workitem', chats: 'chat', agents: 'agent' }
            if (r.type !== typeMap[category]) return false
        }
        if (query && !r.title.toLowerCase().includes(query.toLowerCase()) && !r.description.toLowerCase().includes(query.toLowerCase())) {
            return false
        }
        return true
    })

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
                <Tab value="all">All ({MOCK_RESULTS.length})</Tab>
                <Tab value="projects" icon={<FolderRegular />}>Projects</Tab>
                <Tab value="workitems" icon={<BoardRegular />}>Work Items</Tab>
                <Tab value="chats" icon={<ChatRegular />}>Chats</Tab>
                <Tab value="agents" icon={<BotRegular />}>Agents</Tab>
            </TabList>

            {filtered.length > 0 ? (
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
