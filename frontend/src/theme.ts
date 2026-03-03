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
