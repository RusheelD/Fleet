import { PageShell } from '../../components/shared'
import { PlaybooksTab } from '../SettingsPage/PlaybooksTab'

export function PlaybooksPage() {
  return (
    <PageShell
      title="Playbooks"
      subtitle="Reusable execution patterns deserve first-class space, not a buried settings tab."
      maxWidth="medium"
    >
      <PlaybooksTab />
    </PageShell>
  )
}
