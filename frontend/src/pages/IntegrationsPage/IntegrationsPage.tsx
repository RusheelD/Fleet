import { Spinner } from '@fluentui/react-components'
import { PageShell } from '../../components/shared'
import { useUserSettings } from '../../proxies'
import { ConnectionsTab } from '../SettingsPage/ConnectionsTab'

export function IntegrationsPage() {
  const { data: settings, isLoading } = useUserSettings()

  return (
    <PageShell
      title="Integrations"
      subtitle="Linked accounts, MCP servers, and external systems."
      maxWidth="large"
    >
      {isLoading || !settings ? (
        <Spinner label="Loading integrations..." />
      ) : (
        <ConnectionsTab connections={settings.connections} />
      )}
    </PageShell>
  )
}
