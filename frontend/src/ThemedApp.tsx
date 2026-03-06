import { FluentProvider } from '@fluentui/react-components'
import {
    warmDarkTheme,
    warmLightTheme,
    warmDarkCompactTheme,
    warmLightCompactTheme,
} from './theme'
import { usePreferences } from './hooks'
import { App } from './'

/**
 * Wraps the app in FluentProvider and dynamically switches between
 * light/dark theme based on the user's persisted preference.
 */
export function ThemedApp() {
    const { preferences } = usePreferences()
    const isDark = preferences?.darkMode ?? true
    const isCompact = preferences?.compactMode ?? false
    const theme = isCompact
        ? (isDark ? warmDarkCompactTheme : warmLightCompactTheme)
        : (isDark ? warmDarkTheme : warmLightTheme)

    return (
        <FluentProvider theme={theme} style={{ height: '100%' }}>
            <App />
        </FluentProvider>
    )
}
