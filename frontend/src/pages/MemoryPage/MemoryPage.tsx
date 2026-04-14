import { PageShell } from '../../components/shared'
import { MemoryTab } from '../SettingsPage/MemoryTab'

export function MemoryPage() {
  return (
    <PageShell
      title="Memory"
      subtitle="Keep durable context, working preferences, and important references somewhere Fleet can actually find them."
      maxWidth="medium"
    >
      <MemoryTab />
    </PageShell>
  )
}
