import { StrictMode, useEffect, useState } from 'react'
import { createRoot } from 'react-dom/client'
import { FluentProvider } from '@fluentui/react-components'
import './index.css'
import { App } from './App'
import { warmDarkTheme, warmLightTheme } from './theme'

function Root() {
    const [prefersDark, setPrefersDark] = useState(() => window.matchMedia('(prefers-color-scheme: dark)').matches)

    useEffect(() => {
        const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)')
        const handleThemeChange = (event: MediaQueryListEvent) => {
            setPrefersDark(event.matches)
        }

        setPrefersDark(mediaQuery.matches)
        document.documentElement.dataset.theme = mediaQuery.matches ? 'dark' : 'light'

        if (typeof mediaQuery.addEventListener === 'function') {
            mediaQuery.addEventListener('change', handleThemeChange)
            return () => mediaQuery.removeEventListener('change', handleThemeChange)
        }

        mediaQuery.addListener(handleThemeChange)
        return () => mediaQuery.removeListener(handleThemeChange)
    }, [])

    useEffect(() => {
        document.documentElement.dataset.theme = prefersDark ? 'dark' : 'light'
    }, [prefersDark])

    return (
        <FluentProvider theme={prefersDark ? warmDarkTheme : warmLightTheme} style={{ minHeight: '100%' }}>
            <App />
        </FluentProvider>
    )
}

createRoot(document.getElementById('root')!).render(
    <StrictMode>
        <Root />
    </StrictMode>,
)
