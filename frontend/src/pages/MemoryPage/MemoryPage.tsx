import { PageShell } from '../../components/shared'
import { MemoryTab } from '../SettingsPage/MemoryTab'

export function MemoryPage() {
  return (
    <PageShell
      title="Memory"
      subtitle="Saved context and references that should stay available across sessions."
      maxWidth="medium"
    >
      <MemoryTab />
    </PageShell>
  )
}
