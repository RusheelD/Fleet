import { get } from './'
import type { SubscriptionData } from '../models'

export function getSubscription(): Promise<SubscriptionData> {
  return get<SubscriptionData>('/api/subscription')
}
