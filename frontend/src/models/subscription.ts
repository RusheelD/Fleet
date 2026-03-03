export interface CurrentPlan {
  name: string
  description: string
}

export interface UsageMeter {
  label: string
  usage: string
  value: number
  color: 'brand' | 'warning'
  remaining: string
}

export interface Plan {
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

export interface SubscriptionData {
  currentPlan: CurrentPlan
  usage: UsageMeter[]
  plans: Plan[]
}
