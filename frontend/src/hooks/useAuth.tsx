import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useIsAuthenticated, useMsal } from '@azure/msal-react'
import { InteractionRequiredAuthError, InteractionStatus } from '@azure/msal-browser'
import { apiLoginRequest, getLoginRequest, type AuthProvider } from '../auth'
import { setTokenGetter, get } from '../proxies/proxy'
import { AuthContext, type AuthContextValue } from './AuthContext'
import type { UserProfile } from '../models'

interface AuthProviderProps {
    children: ReactNode
}

const LastAuthProviderStorageKey = 'fleet.last-auth-provider'

export function AuthProvider({ children }: AuthProviderProps) {
    const { instance, inProgress } = useMsal()
    const isAuthenticated = useIsAuthenticated()
    const [user, setUser] = useState<UserProfile | null>(null)
    const [isLoading, setIsLoading] = useState(false)
    const fetchedRef = useRef(false)
    const redirectingRef = useRef(false)
    const lastProviderRef = useRef<AuthProvider>('microsoft')
    // Deduplicate concurrent acquireTokenSilent calls - all callers share one in-flight promise.
    const tokenPromiseRef = useRef<Promise<string | undefined> | null>(null)

    useEffect(() => {
        const persistedProvider = window.sessionStorage.getItem(LastAuthProviderStorageKey)
        if (persistedProvider === 'microsoft' || persistedProvider === 'google' || persistedProvider === 'github') {
            lastProviderRef.current = persistedProvider
        }
    }, [])

    const login = useCallback(async (provider?: AuthProvider) => {
        if (inProgress === InteractionStatus.None) {
            const resolvedProvider: AuthProvider = provider ?? 'microsoft'
            lastProviderRef.current = resolvedProvider
            window.sessionStorage.setItem(LastAuthProviderStorageKey, resolvedProvider)
            const request = getLoginRequest(resolvedProvider)
            await instance.loginRedirect(request)
        }
    }, [instance, inProgress])

    const logout = useCallback(() => {
        setUser(null)
        fetchedRef.current = false
        redirectingRef.current = false
        lastProviderRef.current = 'microsoft'
        window.sessionStorage.removeItem(LastAuthProviderStorageKey)
        void instance.logoutRedirect({ postLogoutRedirectUri: '/' })
    }, [instance])

    const getAccessToken = useCallback(async (): Promise<string | undefined> => {
        const accounts = instance.getAllAccounts()
        if (accounts.length === 0) return undefined

        // If there's already an in-flight token request, piggyback on it
        // instead of spawning another acquireTokenSilent (which opens a
        // hidden iframe and causes sandbox navigation errors).
        if (tokenPromiseRef.current) {
            return tokenPromiseRef.current
        }

        const tokenPromise = (async () => {
            try {
                const result = await instance.acquireTokenSilent({
                    ...apiLoginRequest,
                    account: accounts[0],
                })
                return result.accessToken
            } catch (error: unknown) {
                // Only redirect for genuine interaction-required errors (consent, MFA, expired session).
                // All other failures (timing, cache, network) should NOT trigger a redirect -
                // that creates an infinite login loop when multiple API calls fire concurrently.
                if (error instanceof InteractionRequiredAuthError) {
                    // Guard against multiple concurrent redirects.
                    if (!redirectingRef.current && inProgress === InteractionStatus.None) {
                        redirectingRef.current = true
                        await instance.loginRedirect(getLoginRequest(lastProviderRef.current))
                    }
                } else {
                    console.warn('Silent token acquisition failed (non-interactive):', error)
                }
                return undefined
            }
        })()

        tokenPromiseRef.current = tokenPromise

        try {
            return await tokenPromise
        } finally {
            tokenPromiseRef.current = null
        }
    }, [instance, inProgress])

    // Wire the token getter into the proxy layer so API calls include the Bearer token.
    useEffect(() => {
        setTokenGetter(getAccessToken)
    }, [getAccessToken])

    // When the user is authenticated, call GET /api/auth/me to ensure the
    // backend UserProfile exists (auto-provisioned on first call) and load
    // the profile into context so components can access it.
    useEffect(() => {
        if (!isAuthenticated || inProgress !== InteractionStatus.None || fetchedRef.current) {
            return
        }

        let cancelled = false
        fetchedRef.current = true
        setIsLoading(true)

        // Ensure we have a valid access token before calling the API,
        // so the Bearer header is ready on the very first request.
        getAccessToken()
            .then((token) => {
                if (cancelled || !token) {
                    fetchedRef.current = false
                    return
                }
                return get<UserProfile>('/api/auth/me')
            })
            .then((profile) => {
                if (!cancelled && profile) {
                    setUser(profile)
                }
            })
            .catch((err) => {
                fetchedRef.current = false
                console.error('Failed to load user profile:', err)
            })
            .finally(() => {
                if (!cancelled) {
                    setIsLoading(false)
                }
            })

        return () => { cancelled = true }
    }, [isAuthenticated, inProgress, getAccessToken])

    // Clear user state when MSAL reports not authenticated (e.g. session expired).
    useEffect(() => {
        if (!isAuthenticated && user) {
            setUser(null)
            fetchedRef.current = false
        }
    }, [isAuthenticated, user])

    const updateUser = useCallback((profile: UserProfile) => {
        setUser(profile)
    }, [])

    const value = useMemo<AuthContextValue>(() => ({
        isAuthenticated,
        isLoading,
        user,
        updateUser,
        login,
        logout,
        getAccessToken,
    }), [isAuthenticated, isLoading, user, updateUser, login, logout, getAccessToken])

    return (
        <AuthContext value={value}>
            {children}
        </AuthContext>
    )
}
