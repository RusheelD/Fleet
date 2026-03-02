import type { ReactNode } from 'react'

export interface PlanData {
  name: string
  icon: ReactNode
  price: string
  period: string
  description: string
  features: string[]
  buttonLabel: string
  isCurrent: boolean
  buttonAppearance: 'outline' | 'primary'
}
