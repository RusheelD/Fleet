export interface PlanData {
  name: string
  icon: string
  price: string
  period: string
  description: string
  features: string[]
  buttonLabel: string
  isCurrent: boolean
  buttonAppearance: 'outline' | 'primary'
}
