import { useMemo, useState } from 'react'
import {
  Badge,
  Button,
  Card,
  Caption1,
  Divider,
  Dropdown,
  Field,
  Input,
  Option,
  Switch,
  Text,
  Textarea,
  Title3,
  makeStyles,
  mergeClasses,
} from '@fluentui/react-components'
import type { MemoryEntry } from '../../models'
import { getApiErrorMessage, type UpsertMemoryEntryRequest } from '../../proxies'
import { useIsMobile } from '../../hooks'
import { appTokens } from '../../styles/appTokens'

const memoryTypeOptions: Array<{ value: UpsertMemoryEntryRequest['type']; label: string }> = [
  { value: 'user', label: 'User' },
  { value: 'feedback', label: 'Feedback' },
  { value: 'project', label: 'Project' },
  { value: 'reference', label: 'Reference' },
]

interface MemoryWorkspaceProps {
  title: string
  subtitle: string
  memories?: MemoryEntry[]
  isLoading?: boolean
  isSaving?: boolean
  emptyMessage: string
  createLabel: string
  onCreate: (request: UpsertMemoryEntryRequest) => Promise<unknown>
  onUpdate: (id: number, request: UpsertMemoryEntryRequest) => Promise<unknown>
  onDelete: (id: number) => Promise<unknown>
}

interface DraftState {
  name: string
  description: string
  type: UpsertMemoryEntryRequest['type']
  content: string
  alwaysInclude: boolean
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
  memoryList: {
    display: 'flex',
    flexDirection: 'column',
    gap: appTokens.space.md,
  },
  memoryCard: {
    padding: appTokens.space.md,
    display: 'flex',
    flexDirection: 'column',
    gap: appTokens.space.sm,
    backgroundColor: appTokens.color.pageBackground,
  },
  memoryHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    gap: appTokens.space.md,
    flexWrap: 'wrap',
  },
  memoryBadges: {
    display: 'flex',
    flexWrap: 'wrap',
    gap: appTokens.space.xs,
  },
  memoryActions: {
    display: 'flex',
    gap: appTokens.space.sm,
    flexWrap: 'wrap',
  },
  memoryBody: {
    whiteSpace: 'pre-wrap',
  },
  staleText: {
    color: appTokens.color.warning,
  },
  form: {
    display: 'flex',
    flexDirection: 'column',
    gap: appTokens.space.md,
  },
  formActions: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    gap: appTokens.space.sm,
    flexWrap: 'wrap',
  },
  twoColumn: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
    gap: appTokens.space.md,
  },
  helperText: {
    color: appTokens.color.textMuted,
  },
  errorText: {
    color: appTokens.color.danger,
  },
})

function createEmptyDraft(): DraftState {
  return {
    name: '',
    description: '',
    type: 'feedback',
    content: '',
    alwaysInclude: false,
  }
}

function createDraftFromMemory(memory: MemoryEntry): DraftState {
  return {
    name: memory.name,
    description: memory.description,
    type: memory.type,
    content: memory.content,
    alwaysInclude: memory.alwaysInclude,
  }
}

function formatTimestamp(value: string) {
  return new Date(value).toLocaleDateString()
}

function createPreview(value: string, maxLength = 360) {
  if (value.length <= maxLength) {
    return value
  }

  return `${value.slice(0, maxLength).trimEnd()}...`
}

export function MemoryWorkspace({
  title,
  subtitle,
  memories,
  isLoading = false,
  isSaving = false,
  emptyMessage,
  createLabel,
  onCreate,
  onUpdate,
  onDelete,
}: MemoryWorkspaceProps) {
  const styles = useStyles()
  const isMobile = useIsMobile()
  const [editingId, setEditingId] = useState<number | null>(null)
  const [draft, setDraft] = useState<DraftState>(createEmptyDraft())
  const [error, setError] = useState<string | null>(null)
  const isEditing = editingId !== null

  const orderedMemories = useMemo(
    () => [...(memories ?? [])].sort((left, right) => new Date(right.updatedAtUtc).getTime() - new Date(left.updatedAtUtc).getTime()),
    [memories],
  )

  const resetDraft = () => {
    setEditingId(null)
    setDraft(createEmptyDraft())
    setError(null)
  }

  const handleEdit = (memory: MemoryEntry) => {
    setEditingId(memory.id)
    setDraft(createDraftFromMemory(memory))
    setError(null)
  }

  const handleDelete = async (memory: MemoryEntry) => {
    const confirmed = window.confirm(`Delete "${memory.name}"?`)
    if (!confirmed) {
      return
    }

    try {
      await onDelete(memory.id)
      if (editingId === memory.id) {
        resetDraft()
      }
    } catch (deleteError) {
      setError(getApiErrorMessage(deleteError, `Unable to delete ${memory.name}.`))
    }
  }

  const handleSubmit = async () => {
    setError(null)

    if (!draft.name.trim() || !draft.description.trim() || !draft.content.trim()) {
      setError('Name, description, and memory content are required.')
      return
    }

    const payload: UpsertMemoryEntryRequest = {
      name: draft.name.trim(),
      description: draft.description.trim(),
      type: draft.type,
      content: draft.content.trim(),
      alwaysInclude: draft.alwaysInclude,
    }

    try {
      if (editingId !== null) {
        await onUpdate(editingId, payload)
      } else {
        await onCreate(payload)
      }

      resetDraft()
    } catch (submitError) {
      setError(getApiErrorMessage(submitError, 'Unable to save memory.'))
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

      <Divider />

      <div className={styles.memoryList}>
        {isLoading ? (
          <Text>Loading memories...</Text>
        ) : orderedMemories.length === 0 ? (
          <Text>{emptyMessage}</Text>
        ) : (
          orderedMemories.map((memory) => (
            <Card key={memory.id} className={styles.memoryCard}>
              <div className={styles.memoryHeader}>
                <div>
                  <Text weight="semibold">{memory.name}</Text>
                  <Caption1>{memory.description}</Caption1>
                </div>
                <div className={styles.memoryActions}>
                  <Button appearance="secondary" size="small" onClick={() => handleEdit(memory)} disabled={isSaving}>
                    Edit
                  </Button>
                  <Button appearance="subtle" size="small" onClick={() => void handleDelete(memory)} disabled={isSaving}>
                    Delete
                  </Button>
                </div>
              </div>

              <div className={styles.memoryBadges}>
                <Badge appearance="outline">{memory.type}</Badge>
                <Badge appearance="outline">{memory.scope}</Badge>
                {memory.alwaysInclude ? <Badge color="important">Pinned</Badge> : null}
              </div>

              <Text size={200} className={styles.memoryBody}>{createPreview(memory.content)}</Text>
              <Caption1>Updated {formatTimestamp(memory.updatedAtUtc)}</Caption1>
              {memory.stalenessMessage ? (
                <Caption1 className={styles.staleText}>{memory.stalenessMessage}</Caption1>
              ) : null}
            </Card>
          ))
        )}
      </div>

      <Divider />

      <div className={styles.form}>
        <div>
          <Text weight="semibold">{isEditing ? 'Edit memory' : 'Add memory'}</Text>
          <Caption1 className={styles.helperText}>
            Keep the description concise so Fleet can decide when this memory matters.
          </Caption1>
        </div>

        <div className={styles.twoColumn}>
          <Field label="Name" required>
            <Input value={draft.name} onChange={(_event, data) => setDraft((current) => ({ ...current, name: data.value }))} />
          </Field>
          <Field label="Type" required>
            <Dropdown
              value={memoryTypeOptions.find((option) => option.value === draft.type)?.label ?? draft.type}
              selectedOptions={[draft.type]}
              onOptionSelect={(_event, data) => setDraft((current) => ({ ...current, type: (data.optionValue ?? 'feedback') as DraftState['type'] }))}
            >
              {memoryTypeOptions.map((option) => (
                <Option key={option.value} value={option.value}>
                  {option.label}
                </Option>
              ))}
            </Dropdown>
          </Field>
        </div>

        <Field label="Description" required>
          <Input value={draft.description} onChange={(_event, data) => setDraft((current) => ({ ...current, description: data.value }))} />
        </Field>

        <Field label="Memory content" required hint="Use exact dates for deadlines or milestones so the note stays interpretable later.">
          <Textarea
            resize="vertical"
            rows={8}
            value={draft.content}
            onChange={(_event, data) => setDraft((current) => ({ ...current, content: data.value }))}
          />
        </Field>

        <Switch
          checked={draft.alwaysInclude}
          label="Always include this memory when it might affect the answer"
          onChange={(_event, data) => setDraft((current) => ({ ...current, alwaysInclude: data.checked }))}
        />

        <div className={styles.formActions}>
          <div>
            {error ? <Caption1 className={styles.errorText}>{error}</Caption1> : null}
          </div>
          <div className={styles.memoryActions}>
            {isEditing ? (
              <Button appearance="secondary" onClick={resetDraft} disabled={isSaving}>
                Cancel
              </Button>
            ) : null}
            <Button appearance="primary" onClick={() => void handleSubmit()} disabled={isSaving}>
              {isSaving ? 'Saving...' : isEditing ? 'Update Memory' : 'Save Memory'}
            </Button>
          </div>
        </div>
      </div>
    </Card>
  )
}
