/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_ENTRA_CLIENT_ID: string
  readonly VITE_ENTRA_AUTHORITY: string
  readonly VITE_ENTRA_API_SCOPE: string
  readonly VITE_ENTRA_REDIRECT_URI?: string
  readonly VITE_ENTRA_KNOWN_AUTHORITIES?: string
  readonly VITE_ENTRA_GOOGLE_AUTHORITY?: string
  readonly VITE_ENTRA_GOOGLE_DOMAIN_HINT?: string
  readonly VITE_ENTRA_GOOGLE_IDP_HINT?: string
  readonly VITE_ENVIRONMENT: string
  readonly VITE_WEBSITE_URL: string
  readonly VITE_GITHUB_CLIENT_ID?: string
  readonly VITE_API_ORIGIN?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
