import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MsalProvider } from '@azure/msal-react'
import './index.css'
import { AuthProvider, PreferencesProvider } from './hooks'
import { msalInstance } from './auth'
import { ThemedApp } from './ThemedApp'
import { installStaleChunkRecovery } from './utils/staleChunkRecovery'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: 1,
    },
  },
})

// Initialize MSAL before rendering
installStaleChunkRecovery()

msalInstance.initialize().then(() => {
  // Handle redirect promise from login redirect
  msalInstance.handleRedirectPromise().then(() => {
    createRoot(document.getElementById('root')!).render(
      <StrictMode>
        <QueryClientProvider client={queryClient}>
          <MsalProvider instance={msalInstance}>
            <AuthProvider>
              <PreferencesProvider>
                <ThemedApp />
              </PreferencesProvider>
            </AuthProvider>
          </MsalProvider>
        </QueryClientProvider>
      </StrictMode>,
    )
  })
})
