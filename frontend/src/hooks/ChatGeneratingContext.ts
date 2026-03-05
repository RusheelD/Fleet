import { createContext, useContext } from 'react'

export interface ChatGeneratingContextValue {
  /** Whether the chat is currently generating work items */
  isGenerating: boolean
  /** Set the generating state — called by ChatDrawer */
  setIsGenerating: (value: boolean) => void
}

export const ChatGeneratingContext = createContext<ChatGeneratingContextValue | undefined>(undefined)

export function useChatGenerating(): ChatGeneratingContextValue {
  const ctx = useContext(ChatGeneratingContext)
  if (!ctx) {
    throw new Error('useChatGenerating must be used within a ChatGeneratingProvider')
  }
  return ctx
}
