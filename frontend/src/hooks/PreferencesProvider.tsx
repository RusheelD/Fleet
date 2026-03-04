import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { PreferencesContext, type PreferencesContextValue } from './PreferencesContext'
import { useUserSettings, useUpdatePreferences } from '../proxies'
import type { UserPreferences } from '../models'
import { useAuth } from './useAuthHook'

interface PreferencesProviderProps {
    children: ReactNode
}

/** Default preferences used before the user's preferences are loaded */
const defaults: UserPreferences = {
    agentCompletedNotification: true,
    prOpenedNotification: true,
    agentErrorsNotification: true,
    workItemUpdatesNotification: true,
    darkMode: true,
    compactMode: false,
    sidebarCollapsed: false,
}

export function PreferencesProvider({ children }: PreferencesProviderProps) {
    const { isAuthenticated } = useAuth()
    const queryClient = useQueryClient()
    const { data: settings, isError, refetch } = useUserSettings(isAuthenticated)
    const updateMutation = useUpdatePreferences()
    const [local, setLocal] = useState<UserPreferences>(defaults)
    const hasLoadedPreferencesRef = useRef(false)

    // When authentication completes, explicitly refetch preferences to ensure fresh data
    useEffect(() => {
        if (isAuthenticated && !hasLoadedPreferencesRef.current) {
            console.debug('[PreferencesProvider] User authenticated, refreshing preferences from backend')
            void refetch()
        }
    }, [isAuthenticated, refetch])

    // Sync local state when settings arrive from the server
    useEffect(() => {
        if (settings?.preferences) {
            console.debug('[PreferencesProvider] Loaded user preferences from backend', {
                darkMode: settings.preferences.darkMode,
            })
            setLocal(settings.preferences)
            hasLoadedPreferencesRef.current = true
        }
    }, [settings?.preferences])

    // Log errors to help debug fetch failures
    useEffect(() => {
        if (isError && isAuthenticated && !hasLoadedPreferencesRef.current) {
            console.warn('[PreferencesProvider] Failed to load user preferences from backend')
        }
    }, [isError, isAuthenticated])

    // Reset loaded flag when user logs out (so preferences default to system preference until reloaded)
    useEffect(() => {
        if (!isAuthenticated) {
            hasLoadedPreferencesRef.current = false
            setLocal(defaults)
            // Clear the cached query so it refetches on next login
            queryClient.removeQueries({ queryKey: ['user-settings'] })
        }
    }, [isAuthenticated, queryClient])

    const updatePreference = useCallback(
        (key: keyof UserPreferences, value: boolean) => {
            setLocal((prev) => {
                const next = { ...prev, [key]: value }
                console.debug('[PreferencesProvider] Updated local preference', { key, value })
                updateMutation.mutate(next, {
                    onSuccess: () => {
                        console.debug('[PreferencesProvider] Successfully persisted preference to backend', { key, value })
                    },
                    onError: (error) => {
                        console.error('[PreferencesProvider] Failed to persist preference to backend', { key, value, error })
                    }
                })
                return next
            })
        },
        [updateMutation],
    )

    const value: PreferencesContextValue = {
        preferences: local,
        updatePreference,
    }

    return (
        <PreferencesContext value={value}>
            {children}
        </PreferencesContext>
    )
}
