import { useState } from 'react'
import { makeStyles, tokens } from '@fluentui/react-components'

import { ChatDrawerHeader } from './ChatDrawerHeader'
import { ChatSessionBar } from './ChatSessionBar'
import { ChatMessage } from './ChatMessage'
import { ChatSuggestions } from './ChatSuggestions'
import { ChatInput } from './ChatInput'

import type { ChatMessageData, ChatSessionData } from '../../models'

const MOCK_SESSIONS: ChatSessionData[] = [
    { id: 'c1', title: 'Product Spec Discussion', lastMessage: 'I\'ve generated 12 work items based on your specification...', timestamp: '2 hours ago', isActive: true },
    { id: 'c2', title: 'Auth Implementation Plan', lastMessage: 'The OAuth flow should follow these steps...', timestamp: '1 day ago', isActive: false },
    { id: 'c3', title: 'Database Schema Design', lastMessage: 'Here\'s the proposed PostgreSQL schema...', timestamp: '3 days ago', isActive: false },
    { id: 'c4', title: 'API Endpoint Planning', lastMessage: 'I recommend the following REST endpoints...', timestamp: '1 week ago', isActive: false },
]

const MOCK_MESSAGES: ChatMessageData[] = [
    {
        id: '1',
        role: 'user',
        content: 'I want to build a project management tool that uses AI agents to complete software tasks. The agents should be able to create PRs in GitHub.',
        timestamp: '2:30 PM',
    },
    {
        id: '2',
        role: 'assistant',
        content: 'That sounds like a great project! Let me understand your requirements better. Based on what you\'ve described, here are some key areas I\'d want to cover:\n\n**Core Features:**\n• Project creation with GitHub repo linking\n• AI-powered spec generation through conversation\n• Work item management (similar to Azure DevOps)\n• Multi-agent execution with manager/worker hierarchy\n• Automated PR creation and code review\n\n**Would you like me to:**\n1. Generate a detailed product specification document?\n2. Create work items directly from this conversation?\n3. Discuss the technical architecture first?\n\nLet me know how you\'d like to proceed, and I\'ll help shape the product vision.',
        timestamp: '2:31 PM',
    },
    {
        id: '3',
        role: 'user',
        content: 'Let\'s create work items. I want authentication with GitHub OAuth, a project dashboard, a Kanban-style work item board, and an agent monitoring page.',
        timestamp: '2:35 PM',
    },
    {
        id: '4',
        role: 'assistant',
        content: 'I\'ve generated 12 work items based on your specification. Here\'s a summary:\n\n**Authentication (Priority 1)**\n• Set up GitHub OAuth sign-in flow\n• Implement session management & token storage\n\n**Project Dashboard (Priority 1)**\n• Create project metrics overview\n• Build recent activity feed\n• Add agent status widgets\n\n**Work Item Board (Priority 2)**\n• Implement Kanban board view\n• Add list/backlog view\n• Build work item creation dialog\n• Add drag-and-drop reordering\n\n**Agent Monitor (Priority 2)**\n• Create agent status dashboard\n• Implement real-time log streaming\n• Build agent execution history\n\nAll work items have been added to your board. You can view and edit them in the **Work Items** tab. Would you like me to assign agents to start working on any of these?',
        timestamp: '2:36 PM',
    },
]

const SUGGESTIONS = [
    'Assign agents to auth work items',
    'Generate spec document',
    'Add more work items',
    'Show repo structure',
]

const useStyles = makeStyles({
    drawer: {
        width: '420px',
        borderLeft: `1px solid ${tokens.colorNeutralStroke2}`,
        display: 'flex',
        flexDirection: 'column',
        backgroundColor: tokens.colorNeutralBackground1,
        flexShrink: 0,
        height: '100%',
        overflow: 'hidden',
    },
    messagesContainer: {
        flex: 1,
        overflow: 'auto',
        padding: '1rem',
        display: 'flex',
        flexDirection: 'column',
        gap: '1rem',
    },
})

interface ChatDrawerProps {
    onClose: () => void
}

export function ChatDrawer({ onClose }: ChatDrawerProps) {
    const styles = useStyles()
    const [message, setMessage] = useState('')
    const [activeSession, setActiveSession] = useState('c1')

    return (
        <div className={styles.drawer}>
            <ChatDrawerHeader onClose={onClose} />

            <ChatSessionBar
                sessions={MOCK_SESSIONS}
                activeSessionId={activeSession}
                onSelectSession={setActiveSession}
            />

            <div className={styles.messagesContainer}>
                {MOCK_MESSAGES.map((msg) => (
                    <ChatMessage key={msg.id} message={msg} />
                ))}
            </div>

            <ChatSuggestions suggestions={SUGGESTIONS} onSelect={setMessage} />

            <ChatInput value={message} onChange={setMessage} />
        </div>
    )
}
