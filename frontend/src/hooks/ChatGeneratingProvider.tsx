import { useState, type ReactNode } from 'react'
import { ChatGeneratingContext } from './ChatGeneratingContext'

interface ChatGeneratingProviderProps {
    children: ReactNode
}

export function ChatGeneratingProvider({ children }: ChatGeneratingProviderProps) {
    const [isGenerating, setIsGenerating] = useState(false)

    return (
        <ChatGeneratingContext value={{ isGenerating, setIsGenerating }}>
            {children}
        </ChatGeneratingContext>
    )
}
