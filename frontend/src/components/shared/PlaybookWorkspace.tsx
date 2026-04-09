import { useMemo, useState } from 'react'
import {
  Badge,
  Button,
  Card,
  Caption1,
  Divider,
  Field,
  Input,
  Switch,
  Text,
  Textarea,
  Title3,
  makeStyles,
  mergeClasses,
} from '@fluentui/react-components'
import type { PromptSkill, PromptSkillTemplate } from '../../models'
import { getApiErrorMessage } from '../../proxies/proxy'
import type { UpsertPromptSkillRequest } from '../../proxies/userProxy'
import { useIsMobile } from '../../hooks'
import { appTokens } from '../../styles/appTokens'

interface PlaybookWorkspaceProps {
  title: string
  subtitle: string
  playbooks?: PromptSkill[]
  templates?: PromptSkillTemplate[]
  isLoading?: boolean
  isSaving?: boolean
  emptyMessage: string
  createLabel: string
  onCreate: (request: UpsertPromptSkillRequest) => Promise<unknown>
  onUpdate: (id: number, request: UpsertPromptSkillRequest) => Promise<unknown>
  onDelete: (id: number) => Promise<unknown>
}

interface DraftState {
  name: string
  description: string
  whenToUse: string
  content: string
  enabled: boolean
}

const useStyles = makeStyles({
  section: {
    padding: `calc(${appTokens.space.lg} + ${appTokens.space.xxs})`,
    display: 'flex',
    flexDirection: 'column',
    gap: appTokens.space.lg,
  },
  sectionMobile: {
    paddingTop: appTokens.space.pageYMobile,
    paddingBottom: appTokens.space.pageYMobile,
    paddingLeft: appTokens.space.pageXMobile,
    paddingRight: appTokens.space.pageXMobile,
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    gap: appTokens.space.md,
    flexWrap: 'wrap',
  },
  templateGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
    gap: appTokens.space.md,
  },
  templateCard: {
    padding: appTokens.space.md,
    display: 'flex',
    flexDirection: 'column',
    gap: appTokens.space.sm,
    backgroundColor: appTokens.color.pageBackground,
  },
  playbookList: {
    display: 'flex',
    flexDirection: 'column',
    gap: appTokens.space.md,
  },
  playbookCard: {
    padding: appTokens.space.md,
    display: 'flex',
    flexDirection: 'column',
    gap: appTokens.space.sm,
    backgroundColor: appTokens.color.pageBackground,
  },
  playbookHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    gap: appTokens.space.md,
    flexWrap: 'wrap',
  },
  playbookActions: {
    display: 'flex',
    gap: appTokens.space.sm,
    flexWrap: 'wrap',
  },
  playbookBadges: {
    display: 'flex',
    gap: appTokens.space.xs,
    flexWrap: 'wrap',
  },
  bodyPreview: {
    whiteSpace: 'pre-wrap',
  },
  form: {
    display: 'flex',
    flexDirection: 'column',
    gap: appTokens.space.md,
  },
  helperText: {
    color: appTokens.color.textMuted,
  },
  formActions: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    gap: appTokens.space.sm,
    flexWrap: 'wrap',
  },
  errorText: {
    color: appTokens.color.danger,
  },
})

function createEmptyDraft(): DraftState {
  return {
    name: '',
    description: '',
    whenToUse: '',
    content: '',
    enabled: true,
  }
}

function createDraftFromPlaybook(playbook: PromptSkill): DraftState {
  return {
    name: playbook.name,
    description: playbook.description,
    whenToUse: playbook.whenToUse,
    content: playbook.content,
    enabled: playbook.enabled,
  }
}

function createDraftFromTemplate(template: PromptSkillTemplate): DraftState {
  return {
    name: template.name,
    description: template.description,
    whenToUse: template.whenToUse,
    content: template.content,
    enabled: true,
  }
}

function createPreview(value: string, maxLength = 360) {
  if (value.length <= maxLength) {
    return value
  }

  return `${value.slice(0, maxLength).trimEnd()}...`
}

function formatTimestamp(value: string) {
  return new Date(value).toLocaleDateString()
}

export function PlaybookWorkspace({
  title,
  subtitle,
  playbooks,
  templates,
  isLoading = false,
  isSaving = false,
  emptyMessage,
  createLabel,
  onCreate,
  onUpdate,
  onDelete,
}: PlaybookWorkspaceProps) {
  const styles = useStyles()
  const isMobile = useIsMobile()
  const [editingId, setEditingId] = useState<number | null>(null)
  const [draft, setDraft] = useState<DraftState>(createEmptyDraft())
  const [error, setError] = useState<string | null>(null)
  const isEditing = editingId !== null

  const orderedPlaybooks = useMemo(
    () => [...(playbooks ?? [])].sort((left, right) => new Date(right.updatedAtUtc).getTime() - new Date(left.updatedAtUtc).getTime()),
    [playbooks],
  )

  const resetDraft = () => {
    setEditingId(null)
    setDraft(createEmptyDraft())
    setError(null)
  }

  const handleEdit = (playbook: PromptSkill) => {
    setEditingId(playbook.id)
    setDraft(createDraftFromPlaybook(playbook))
    setError(null)
  }

  const handleDelete = async (playbook: PromptSkill) => {
    const confirmed = window.confirm(`Delete "${playbook.name}"?`)
    if (!confirmed) {
      return
    }

    try {
      await onDelete(playbook.id)
      if (editingId === playbook.id) {
        resetDraft()
      }
    } catch (deleteError) {
      setError(getApiErrorMessage(deleteError, `Unable to delete ${playbook.name}.`))
    }
  }

  const handleSubmit = async () => {
    setError(null)

    if (!draft.name.trim() || !draft.description.trim() || !draft.whenToUse.trim() || !draft.content.trim()) {
      setError('Name, description, when-to-use guidance, and content are required.')
      return
    }

    const payload: UpsertPromptSkillRequest = {
      name: draft.name.trim(),
      description: draft.description.trim(),
      whenToUse: draft.whenToUse.trim(),
      content: draft.content.trim(),
      enabled: draft.enabled,
    }

    try {
      if (editingId !== null) {
        await onUpdate(editingId, payload)
      } else {
        await onCreate(payload)
      }

      resetDraft()
    } catch (submitError) {
      setError(getApiErrorMessage(submitError, 'Unable to save playbook.'))
    }
  }

  return (
    <Card className={mergeClasses(styles.section, isMobile && styles.sectionMobile)}>
      <div className={styles.header}>
        <div>
          <Title3>{title}</Title3>
          <Caption1 className={styles.helperText}>{subtitle}</Caption1>
        </div>
        <Button appearance="secondary" onClick={resetDraft} disabled={isSaving}>
          {createLabel}
        </Button>
      </div>

      {templates && templates.length > 0 ? (
        <>
          <Divider />
          <div>
            <Text weight="semibold">Built-in Playbooks</Text>
            <Caption1 className={styles.helperText}>
              Fleet can already use these built-ins automatically. You can also copy one into the editor as a starting point for a custom playbook.
            </Caption1>
          </div>
          <div className={styles.templateGrid}>
            {templates.map((template) => (
              <Card key={template.key} className={styles.templateCard}>
                <div className={styles.playbookBadges}>
                  <Badge appearance="outline">built-in</Badge>
                </div>
                <Text weight="semibold">{template.name}</Text>
                <Caption1>{template.description}</Caption1>
                <Caption1 className={styles.helperText}>Use when: {template.whenToUse}</Caption1>
                <Button appearance="secondary" size="small" onClick={() => setDraft(createDraftFromTemplate(template))} disabled={isSaving}>
                  Use as Starting Point
                </Button>
              </Card>
            ))}
          </div>
        </>
      ) : null}

      <Divider />

      <div className={styles.playbookList}>
        {isLoading ? (
          <Text>Loading playbooks...</Text>
        ) : orderedPlaybooks.length === 0 ? (
          <Text>{emptyMessage}</Text>
        ) : (
          orderedPlaybooks.map((playbook) => (
            <Card key={playbook.id} className={styles.playbookCard}>
              <div className={styles.playbookHeader}>
                <div>
                  <Text weight="semibold">{playbook.name}</Text>
                  <Caption1>{playbook.description}</Caption1>
                </div>
                <div className={styles.playbookActions}>
                  <Button appearance="secondary" size="small" onClick={() => handleEdit(playbook)} disabled={isSaving}>
                    Edit
                  </Button>
                  <Button appearance="subtle" size="small" onClick={() => void handleDelete(playbook)} disabled={isSaving}>
                    Delete
                  </Button>
                </div>
              </div>

              <div className={styles.playbookBadges}>
                <Badge appearance="outline">{playbook.scope}</Badge>
                {playbook.enabled ? <Badge color="success">Enabled</Badge> : <Badge appearance="outline">Disabled</Badge>}
              </div>

              <Caption1 className={styles.helperText}>Use when: {playbook.whenToUse}</Caption1>
              <Text size={200} className={styles.bodyPreview}>{createPreview(playbook.content)}</Text>
              <Caption1>Updated {formatTimestamp(playbook.updatedAtUtc)}</Caption1>
            </Card>
          ))
        )}
      </div>

      <Divider />

      <div className={styles.form}>
        <div>
          <Text weight="semibold">{isEditing ? 'Edit playbook' : 'Add playbook'}</Text>
          <Caption1 className={styles.helperText}>
            Keep the description and usage guidance crisp so Fleet knows when to apply this playbook.
          </Caption1>
        </div>

        <Field label="Name" required>
          <Input value={draft.name} onChange={(_event, data) => setDraft((current) => ({ ...current, name: data.value }))} />
        </Field>

        <Field label="Description" required>
          <Input value={draft.description} onChange={(_event, data) => setDraft((current) => ({ ...current, description: data.value }))} />
        </Field>

        <Field label="When to use" required hint="Describe the kinds of requests or situations that should activate this playbook.">
          <Textarea
            resize="vertical"
            rows={3}
            value={draft.whenToUse}
            onChange={(_event, data) => setDraft((current) => ({ ...current, whenToUse: data.value }))}
          />
        </Field>

        <Field label="Playbook instructions" required>
          <Textarea
            resize="vertical"
            rows={8}
            value={draft.content}
            onChange={(_event, data) => setDraft((current) => ({ ...current, content: data.value }))}
          />
        </Field>

        <Switch
          checked={draft.enabled}
          label="Enable this playbook"
          onChange={(_event, data) => setDraft((current) => ({ ...current, enabled: data.checked }))}
        />

        <div className={styles.formActions}>
          <div>
            {error ? <Caption1 className={styles.errorText}>{error}</Caption1> : null}
          </div>
          <div className={styles.playbookActions}>
            {isEditing ? (
              <Button appearance="secondary" onClick={resetDraft} disabled={isSaving}>
                Cancel
              </Button>
            ) : null}
            <Button appearance="primary" onClick={() => void handleSubmit()} disabled={isSaving}>
              {isSaving ? 'Saving...' : isEditing ? 'Update Playbook' : 'Save Playbook'}
            </Button>
          </div>
        </div>
      </div>
    </Card>
  )
}
