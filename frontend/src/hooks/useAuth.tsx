import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useIsAuthenticated, useMsal } from '@azure/msal-react'
import { InteractionRequiredAuthError, InteractionStatus, type RedirectRequest } from '@azure/msal-browser'
import {
    apiLoginRequest,
    authConfigError,
    emailLoginRequest,
    emailSignUpRequest,
    googleLoginRequest,
    isAuthConfigured,
    LOGIN_PROVIDER_LINK_ERROR_KEY,
    microsoftLoginRequest,
    PENDING_LOGIN_PROVIDER_LINK_STATE_KEY,
    redirectUri,
} from '../auth'
import type { AuthLoginProvider } from '../auth'
import { setTokenGetter, get, getApiErrorMessage } from '../proxies/proxy'
import { completeLoginProviderLink, createLoginProviderLinkState } from '../proxies/authProxy'
import { AuthContext, type AuthContextValue } from './AuthContext'
import type { UserProfile } from '../models'

interface AuthProviderProps {
    children: ReactNode
}

function resolveLoginRequest(provider?: AuthLoginProvider, signUp = false): RedirectRequest {
    if (provider === 'google') {
        return googleLoginRequest
    }

    if (provider === 'microsoft') {
        return microsoftLoginRequest
    }

    return signUp ? emailSignUpRequest : emailLoginRequest
}

function resolveProviderLinkRequest(provider: AuthLoginProvider): RedirectRequest {
    return {
        ...resolveLoginRequest(provider),
        prompt: 'login',
    }
}

export function AuthProvider({ children }: AuthProviderProps) {
    const { instance, inProgress } = useMsal()
    const isAuthenticated = useIsAuthenticated()
    const [user, setUser] = useState<UserProfile | null>(null)
    const [isLoading, setIsLoading] = useState(false)
    const fetchedRef = useRef(false)
    const redirectingRef = useRef(false)
    // Deduplicate concurrent acquireTokenSilent calls — all callers share one in-flight promise
    const tokenPromiseRef = useRef<Promise<string | undefined> | null>(null)

    const login = useCallback(async (provider?: AuthLoginProvider) => {
        if (!isAuthConfigured) {
            throw new Error(authConfigError ?? 'Fleet sign-in is not configured.')
        }

        if (inProgress === InteractionStatus.None) {
            await instance.loginRedirect(resolveLoginRequest(provider))
        }
    }, [instance, inProgress])

    const signUp = useCallback(async (provider?: AuthLoginProvider) => {
        if (!isAuthConfigured) {
            throw new Error(authConfigError ?? 'Fleet sign-in is not configured.')
        }

        if (inProgress === InteractionStatus.None) {
            await instance.loginRedirect(resolveLoginRequest(provider, true))
        }
    }, [instance, inProgress])

    const linkLoginProvider = useCallback(async (provider: AuthLoginProvider) => {
        if (!isAuthConfigured) {
            throw new Error(authConfigError ?? 'Fleet sign-in is not configured.')
        }

        if (inProgress !== InteractionStatus.None) {
            return
        }

        const { state } = await createLoginProviderLinkState(provider)
        window.sessionStorage.setItem(PENDING_LOGIN_PROVIDER_LINK_STATE_KEY, state)
        await instance.loginRedirect(resolveProviderLinkRequest(provider))
    }, [instance, inProgress])

    const logout = useCallback(() => {
        setUser(null)
        fetchedRef.current = false
        redirectingRef.current = false
        void instance.logoutRedirect({ postLogoutRedirectUri: redirectUri })
    }, [instance])

    const getAccessToken = useCallback(async (): Promise<string | undefined> => {
        if (!isAuthConfigured) {
            return undefined
        }

        const accounts = instance.getAllAccounts()
        const account = instance.getActiveAccount() ?? accounts[0]
        if (!account) return undefined

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
                    account,
                })
                return result.accessToken
            } catch (error: unknown) {
                // Only redirect for genuine interaction-required errors (consent, MFA, expired session).
                // All other failures (timing, cache, network) should NOT trigger a redirect —
                // that creates an infinite login loop when multiple API calls fire concurrently.
                if (error instanceof InteractionRequiredAuthError) {
                    // Guard against multiple concurrent redirects
                    if (!redirectingRef.current && inProgress === InteractionStatus.None) {
                        redirectingRef.current = true
                        await instance.loginRedirect(apiLoginRequest)
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
                const pendingLinkState = window.sessionStorage.getItem(PENDING_LOGIN_PROVIDER_LINK_STATE_KEY)
                if (pendingLinkState) {
                    return completeLoginProviderLink(pendingLinkState)
                        .then((profile) => {
                            window.sessionStorage.removeItem(PENDING_LOGIN_PROVIDER_LINK_STATE_KEY)
                            return profile
                        })
                        .catch((error) => {
                            window.sessionStorage.removeItem(PENDING_LOGIN_PROVIDER_LINK_STATE_KEY)
                            window.sessionStorage.setItem(
                                LOGIN_PROVIDER_LINK_ERROR_KEY,
                                getApiErrorMessage(error, 'Unable to link that sign-in method.'),
                            )
                            void instance.logoutRedirect({ postLogoutRedirectUri: redirectUri })
                            throw error
                        })
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
    }, [isAuthenticated, inProgress, getAccessToken, instance])

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
        isAuthConfigured,
        authConfigError,
        user,
        updateUser,
        login,
        signUp,
        linkLoginProvider,
        logout,
        getAccessToken,
    }), [isAuthenticated, isLoading, user, updateUser, login, signUp, linkLoginProvider, logout, getAccessToken])

    return (
        <AuthContext value={value}>
            {children}
        </AuthContext>
    )
}
