export interface PlanData {
    name: string
    popular?: boolean
    price: string
    period: string
    description: string
    features: string[]
    cta: string
    ctaAppearance: 'primary' | 'outline'
}
