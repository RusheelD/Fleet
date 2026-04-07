import { PlaybookWorkspace } from '../../components/shared'
import {
  useCreateUserSkill,
  useDeleteUserSkill,
  useSkillTemplates,
  useUpdateUserSkill,
  useUserSkills,
  type UpsertPromptSkillRequest,
} from '../../proxies'

export function PlaybooksTab() {
  const templates = useSkillTemplates()
  const playbooks = useUserSkills()
  const createPlaybook = useCreateUserSkill()
  const updatePlaybook = useUpdateUserSkill()
  const deletePlaybook = useDeleteUserSkill()

  const isSaving = createPlaybook.isPending || updatePlaybook.isPending || deletePlaybook.isPending

  const handleCreate = (request: UpsertPromptSkillRequest) => createPlaybook.mutateAsync(request)
  const handleUpdate = (id: number, request: UpsertPromptSkillRequest) => updatePlaybook.mutateAsync({ id, data: request })
  const handleDelete = (id: number) => deletePlaybook.mutateAsync(id)

  return (
    <PlaybookWorkspace
      title="Personal Playbooks"
      subtitle="Create reusable workflows Fleet can automatically apply when you are planning features, triaging bugs, writing status updates, or shaping execution."
      templates={templates.data}
      playbooks={playbooks.data}
      isLoading={templates.isLoading || playbooks.isLoading}
      isSaving={isSaving}
      emptyMessage="You have not created any personal playbooks yet."
      createLabel="New Personal Playbook"
      onCreate={handleCreate}
      onUpdate={handleUpdate}
      onDelete={handleDelete}
    />
  )
}
