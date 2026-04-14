import { PageShell } from '../../components/shared'
import { PlaybooksTab } from '../SettingsPage/PlaybooksTab'

export function PlaybooksPage() {
  return (
    <PageShell
      title="Playbooks"
      subtitle="Reusable instructions for how Fleet should handle recurring kinds of work."
      maxWidth="medium"
    >
      <PlaybooksTab />
    </PageShell>
  )
}
