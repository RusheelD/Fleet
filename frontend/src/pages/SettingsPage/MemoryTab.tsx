import { MemoryWorkspace } from '../../components/shared'
import {
  useCreateUserMemory,
  useDeleteUserMemory,
  useUpdateUserMemory,
  useUserMemories,
  type UpsertMemoryEntryRequest,
} from '../../proxies'

export function MemoryTab() {
  const memories = useUserMemories()
  const createMemory = useCreateUserMemory()
  const updateMemory = useUpdateUserMemory()
  const deleteMemory = useDeleteUserMemory()

  const isSaving = createMemory.isPending || updateMemory.isPending || deleteMemory.isPending

  const handleCreate = (request: UpsertMemoryEntryRequest) => createMemory.mutateAsync(request)
  const handleUpdate = (id: number, request: UpsertMemoryEntryRequest) => updateMemory.mutateAsync({ id, data: request })
  const handleDelete = (id: number) => deleteMemory.mutateAsync(id)

  return (
    <MemoryWorkspace
      title="Personal Memory"
      subtitle="Capture preferences, working agreements, recurring context, and external references that Fleet should remember across chats and runs."
      memories={memories.data}
      isLoading={memories.isLoading}
      isSaving={isSaving}
      emptyMessage="You have not saved any personal memories yet."
      createLabel="New Personal Memory"
      onCreate={handleCreate}
      onUpdate={handleUpdate}
      onDelete={handleDelete}
    />
  )
}
