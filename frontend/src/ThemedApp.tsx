import { FluentProvider } from '@fluentui/react-components'
import { warmDarkTheme, warmLightTheme } from './theme'
import { usePreferences } from './hooks'
import { App } from './'

/**
 * Wraps the app in FluentProvider and dynamically switches between
 * light/dark theme based on the user's persisted preference.
 */
export function ThemedApp() {
    const { preferences } = usePreferences()
    const isDark = preferences?.darkMode ?? window.matchMedia('(prefers-color-scheme: dark)').matches

    return (
        <FluentProvider theme={isDark ? warmDarkTheme : warmLightTheme} style={{ height: '100%' }}>
            <App />
        </FluentProvider>
    )
}
