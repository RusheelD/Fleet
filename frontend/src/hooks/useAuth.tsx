import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useIsAuthenticated, useMsal } from '@azure/msal-react'
import { InteractionStatus } from '@azure/msal-browser'
import { apiLoginRequest, googleLoginRequest, githubLoginRequest } from '../auth'
import { setTokenGetter, get } from '../proxies/proxy'
import { AuthContext, type AuthContextValue } from './AuthContext'
import type { UserProfile } from '../models'

interface AuthProviderProps {
    children: ReactNode
}

export function AuthProvider({ children }: AuthProviderProps) {
    const { instance, inProgress } = useMsal()
    const isAuthenticated = useIsAuthenticated()
    const [user, setUser] = useState<UserProfile | null>(null)
    const [isLoading, setIsLoading] = useState(false)
    const fetchedRef = useRef(false)

    const login = useCallback(async (provider?: 'microsoft' | 'google' | 'github') => {
        if (inProgress === InteractionStatus.None) {
            const request =
                provider === 'google' ? googleLoginRequest :
                    provider === 'github' ? githubLoginRequest :
                        apiLoginRequest
            await instance.loginRedirect(request)
        }
    }, [instance, inProgress])

    const logout = useCallback(() => {
        setUser(null)
        fetchedRef.current = false
        void instance.logoutRedirect({ postLogoutRedirectUri: '/' })
    }, [instance])

    const getAccessToken = useCallback(async (): Promise<string | undefined> => {
        const accounts = instance.getAllAccounts()
        if (accounts.length === 0) return undefined

        try {
            const result = await instance.acquireTokenSilent({
                ...apiLoginRequest,
                account: accounts[0],
            })
            return result.accessToken
        } catch {
            // Silent token acquisition failed — trigger interactive login
            await instance.loginRedirect(apiLoginRequest)
            return undefined
        }
    }, [instance])

    // Wire the token getter into the proxy layer so API calls include the Bearer token
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

    // Clear user state when MSAL reports not authenticated (e.g. session expired)
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
