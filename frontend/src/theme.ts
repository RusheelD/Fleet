import {
    createDarkTheme,
    createLightTheme,
    type BrandVariants,
    type Theme,
} from '@fluentui/react-components'

/**
 * Warm brand palette — orange-amber tones that replace the default blue.
 * Generated to give Fleet a warm, energetic feel.
 */
const warmBrand: BrandVariants = {
    10: '#0A0600',
    20: '#1A1005',
    30: '#2D1A08',
    40: '#3E220A',
    50: '#512C0C',
    60: '#65370E',
    70: '#7A4211',
    80: '#904E14',
    90: '#A65B18',
    100: '#BD691C',
    110: '#D47820',
    120: '#E88A2A',
    130: '#F09A3E',
    140: '#F5AD5E',
    150: '#F9BF7E',
    160: '#FCD09E',
}

export const warmLightTheme: Theme = {
    ...createLightTheme(warmBrand),
}

export const warmDarkTheme: Theme = {
    ...createDarkTheme(warmBrand),
    // Slightly warmer neutral backgrounds for dark mode
    colorNeutralBackground1: '#1C1714',
    colorNeutralBackground2: '#211C18',
    colorNeutralBackground3: '#272018',
    colorNeutralBackground4: '#2D261D',
}

function withCompactDensity(theme: Theme): Theme {
    return {
        ...theme,
        spacingHorizontalXXS: '2px',
        spacingHorizontalXS: '4px',
        spacingHorizontalS: '6px',
        spacingHorizontalM: '8px',
        spacingHorizontalL: '10px',
        spacingHorizontalXL: '14px',
        spacingHorizontalXXL: '18px',
        spacingVerticalXXS: '2px',
        spacingVerticalXS: '4px',
        spacingVerticalS: '6px',
        spacingVerticalM: '8px',
        spacingVerticalL: '10px',
        spacingVerticalXL: '14px',
        spacingVerticalXXL: '18px',
        fontSizeBase100: '11px',
        fontSizeBase200: '12px',
        fontSizeBase300: '13px',
        fontSizeBase400: '14px',
        lineHeightBase100: '14px',
        lineHeightBase200: '16px',
        lineHeightBase300: '18px',
        lineHeightBase400: '20px',
    }
}

export const warmLightCompactTheme: Theme = withCompactDensity(warmLightTheme)
export const warmDarkCompactTheme: Theme = withCompactDensity(warmDarkTheme)
