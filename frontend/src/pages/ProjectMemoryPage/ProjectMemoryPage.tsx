import { Spinner } from '@fluentui/react-components'
import { MemoryWorkspace, PageShell } from '../../components/shared'
import { useCurrentProject } from '../../hooks'
import {
  useCreateProjectMemory,
  useDeleteProjectMemory,
  useProjectMemories,
  useUpdateProjectMemory,
  type UpsertMemoryEntryRequest,
} from '../../proxies'

export function ProjectMemoryPage() {
  const { projectId, projectTitle, isLoading } = useCurrentProject()
  const memories = useProjectMemories(projectId, Boolean(projectId))
  const createMemory = useCreateProjectMemory(projectId)
  const updateMemory = useUpdateProjectMemory(projectId)
  const deleteMemory = useDeleteProjectMemory(projectId)

  const isSaving = createMemory.isPending || updateMemory.isPending || deleteMemory.isPending

  const handleCreate = (request: UpsertMemoryEntryRequest) => createMemory.mutateAsync(request)
  const handleUpdate = (id: number, request: UpsertMemoryEntryRequest) => updateMemory.mutateAsync({ id, data: request })
  const handleDelete = (id: number) => deleteMemory.mutateAsync(id)

  return (
    <PageShell
      title="Project Memory"
      subtitle={projectTitle ? `Saved context for ${projectTitle}.` : 'Saved context for this project.'}
      maxWidth="medium"
    >
      {isLoading || !projectId ? (
        <Spinner label="Loading project memory..." />
      ) : (
        <MemoryWorkspace
          title="Project Memory"
          subtitle="Notes, references, and constraints that should stay attached to this project."
          memories={memories.data}
          isLoading={memories.isLoading}
          isSaving={isSaving}
          emptyMessage="No project memory has been saved yet."
          createLabel="New Project Memory"
          onCreate={handleCreate}
          onUpdate={handleUpdate}
          onDelete={handleDelete}
        />
      )}
    </PageShell>
  )
}
