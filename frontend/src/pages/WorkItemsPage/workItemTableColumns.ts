export type WorkItemTableColumnKey =
  | 'type'
  | 'title'
  | 'state'
  | 'id'
  | 'difficulty'
  | 'assignedTo'
  | 'tags'

export interface WorkItemTableColumnDefinition {
  key: WorkItemTableColumnKey
  label: string
  collapsible: boolean
}

export const WORK_ITEM_TABLE_COLUMNS: WorkItemTableColumnDefinition[] = [
  { key: 'type', label: 'Type', collapsible: true },
  { key: 'title', label: 'Title', collapsible: false },
  { key: 'state', label: 'State', collapsible: true },
  { key: 'id', label: 'ID', collapsible: true },
  { key: 'difficulty', label: 'Difficulty', collapsible: true },
  { key: 'assignedTo', label: 'Assigned To', collapsible: true },
  { key: 'tags', label: 'Tags', collapsible: true },
]

export const DEFAULT_WORK_ITEM_COLUMN_WIDTHS: Record<WorkItemTableColumnKey, number> = {
  type: 110,
  title: 520,
  state: 120,
  id: 65,
  difficulty: 90,
  assignedTo: 170,
  tags: 170,
}

export const MIN_WORK_ITEM_COLUMN_WIDTHS: Record<WorkItemTableColumnKey, number> = {
  type: 80,
  title: 240,
  state: 100,
  id: 55,
  difficulty: 80,
  assignedTo: 120,
  tags: 120,
}

export function buildWorkItemGridTemplateColumns(
  widths: Record<WorkItemTableColumnKey, number>,
  collapsedColumns: ReadonlySet<WorkItemTableColumnKey>,
): string {
  const cols = ['34px']
  for (const column of WORK_ITEM_TABLE_COLUMNS) {
    if (!collapsedColumns.has(column.key)) {
      cols.push(`${Math.max(40, Math.round(widths[column.key]))}px`)
    }
  }
  return cols.join(' ')
}

