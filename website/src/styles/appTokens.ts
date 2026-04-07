import { tokens } from '@fluentui/react-components'
import {
    appTokens as frontendAppTokens,
    APP_MOBILE_MAX_WIDTH,
    APP_MOBILE_MEDIA_QUERY,
} from '../generated/frontend-brand/appTokens'

const cssVar = (name: string) => `var(${name})`

export const WEBSITE_MOBILE_MAX_WIDTH = APP_MOBILE_MAX_WIDTH
export const WEBSITE_TABLET_MAX_WIDTH = 768
export const WEBSITE_MOBILE_MEDIA_QUERY = APP_MOBILE_MEDIA_QUERY

export const appTokens = {
    ...frontendAppTokens,
    space: {
        ...frontendAppTokens.space,
        xxxl: cssVar('--app-space-3xl'),
    },
    fontSize: {
        ...frontendAppTokens.fontSize,
        hero: tokens.fontSizeHero800,
    },
    lineHeight: {
        ...frontendAppTokens.lineHeight,
        hero: tokens.lineHeightHero800,
    },
    fontWeight: {
        regular: tokens.fontWeightRegular,
        ...frontendAppTokens.fontWeight,
    },
}
