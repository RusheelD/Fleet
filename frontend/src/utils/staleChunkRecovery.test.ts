import { describe, expect, it } from 'vitest'
import { getUserFacingErrorMessage, isStaleChunkError } from './staleChunkRecovery'

describe('staleChunkRecovery', () => {
    it('detects stale dynamic import failures', () => {
        expect(isStaleChunkError(new Error('Failed to fetch dynamically imported module'))).toBe(true)
        expect(isStaleChunkError('Importing a module script failed')).toBe(true)
    })

    it('shows a friendly message for stale chunk failures', () => {
        expect(
            getUserFacingErrorMessage(new Error('Failed to fetch dynamically imported module: /assets/LoginPage-abcd1234.js')),
        ).toBe('Fleet updated in the background. Reload the page to continue with the latest version.')
    })

    it('preserves regular error messages', () => {
        expect(getUserFacingErrorMessage(new Error('Network connection lost'))).toBe('Network connection lost')
    })
})
