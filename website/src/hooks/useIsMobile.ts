import { useEffect, useState } from 'react'

const MOBILE_MEDIA_QUERY = '(max-width: 900px)'

function getInitialIsMobile(): boolean {
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
        return false
    }

    return window.matchMedia(MOBILE_MEDIA_QUERY).matches
}

export function useIsMobile(): boolean {
    const [isMobile, setIsMobile] = useState(getInitialIsMobile)

    useEffect(() => {
        if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
            return
        }

        const mediaQuery = window.matchMedia(MOBILE_MEDIA_QUERY)
        const update = () => setIsMobile(mediaQuery.matches)
        update()

        if (typeof mediaQuery.addEventListener === 'function') {
            mediaQuery.addEventListener('change', update)
            return () => mediaQuery.removeEventListener('change', update)
        }

        mediaQuery.addListener(update)
        return () => mediaQuery.removeListener(update)
    }, [])

    return isMobile
}
