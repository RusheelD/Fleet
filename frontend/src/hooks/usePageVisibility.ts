import { useState, useEffect } from 'react'

/** Returns `true` when the page is visible, `false` when the tab is hidden. */
export function usePageVisibility(): boolean {
    const [isVisible, setIsVisible] = useState(!document.hidden)

    useEffect(() => {
        const handler = () => setIsVisible(!document.hidden)
        document.addEventListener('visibilitychange', handler)
        return () => document.removeEventListener('visibilitychange', handler)
    }, [])

    return isVisible
}
