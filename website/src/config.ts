/**
 * Environment-based configuration for the Fleet website.
 * The app URL changes based on the deployment environment.
 */

/** The URL where the Fleet app is hosted (for login/signup redirects) */
export const APP_URL = import.meta.env.VITE_APP_URL as string | undefined ?? 'https://app.fleet-ai.dev'

/** Current environment name for display purposes */
export const ENVIRONMENT = import.meta.env.VITE_ENVIRONMENT as string | undefined ?? 'production'
