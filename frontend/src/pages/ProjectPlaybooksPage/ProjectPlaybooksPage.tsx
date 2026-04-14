import { Spinner } from '@fluentui/react-components'
import { PageShell, PlaybookWorkspace } from '../../components/shared'
import { useCurrentProject } from '../../hooks'
import {
  useCreateProjectSkill,
  useDeleteProjectSkill,
  useProjectSkills,
  useSkillTemplates,
  useUpdateProjectSkill,
  type UpsertPromptSkillRequest,
} from '../../proxies'

export function ProjectPlaybooksPage() {
  const { projectId, projectTitle, isLoading } = useCurrentProject()
  const templates = useSkillTemplates()
  const playbooks = useProjectSkills(projectId, Boolean(projectId))
  const createPlaybook = useCreateProjectSkill(projectId)
  const updatePlaybook = useUpdateProjectSkill(projectId)
  const deletePlaybook = useDeleteProjectSkill(projectId)

  const isSaving = createPlaybook.isPending || updatePlaybook.isPending || deletePlaybook.isPending

  const handleCreate = (request: UpsertPromptSkillRequest) => createPlaybook.mutateAsync(request)
  const handleUpdate = (id: number, request: UpsertPromptSkillRequest) => updatePlaybook.mutateAsync({ id, data: request })
  const handleDelete = (id: number) => deletePlaybook.mutateAsync(id)

  return (
    <PageShell
      title="Project Playbooks"
      subtitle={projectTitle ? `Reusable instructions for ${projectTitle}.` : 'Reusable instructions for this project.'}
      maxWidth="medium"
    >
      {isLoading || !projectId ? (
        <Spinner label="Loading project playbooks..." />
      ) : (
        <PlaybookWorkspace
          title="Project Playbooks"
          subtitle="Project-specific instructions and workflows Fleet should reuse here."
          templates={templates.data}
          playbooks={playbooks.data}
          isLoading={templates.isLoading || playbooks.isLoading}
          isSaving={isSaving}
          emptyMessage="No project playbooks have been created yet."
          createLabel="New Project Playbook"
          onCreate={handleCreate}
          onUpdate={handleUpdate}
          onDelete={handleDelete}
        />
      )}
    </PageShell>
  )
}
