/**
 * Environment-based configuration for the Fleet website.
 * The app URL changes based on the deployment environment.
 */

const PLACEHOLDER_APP_URL = 'https://app.your-domain.com'
const DEFAULT_APP_URL = 'https://fleet-ai-dev.azurewebsites.net'

function normalizeAppUrl(rawValue?: string): string {
    const value = rawValue?.trim()
    if (!value || value === PLACEHOLDER_APP_URL) {
        return DEFAULT_APP_URL
    }

    return value.replace(/\/+$/, '')
}

/** The URL where the Fleet app is hosted (for login/signup redirects) */
export const APP_URL = normalizeAppUrl(import.meta.env.VITE_APP_URL as string | undefined)

/** Current environment name for display purposes */
export const ENVIRONMENT = (import.meta.env.VITE_ENVIRONMENT as string | undefined)?.trim() || 'production'
