import { useEffect, useState } from 'react'
import { APP_MOBILE_MEDIA_QUERY } from '../styles/appTokens'

function getInitialIsMobile(): boolean {
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
        return false
    }

    return window.matchMedia(APP_MOBILE_MEDIA_QUERY).matches
}

export function useIsMobile(): boolean {
    const [isMobile, setIsMobile] = useState(getInitialIsMobile)

    useEffect(() => {
        if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
            return
        }

        const mediaQuery = window.matchMedia(APP_MOBILE_MEDIA_QUERY)
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
