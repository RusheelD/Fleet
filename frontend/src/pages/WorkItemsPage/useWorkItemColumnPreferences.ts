import { useCallback, useEffect, useState } from 'react'
import {
    DEFAULT_WORK_ITEM_COLUMN_WIDTHS,
    MIN_WORK_ITEM_COLUMN_WIDTHS,
    WORK_ITEM_TABLE_COLUMNS,
    type WorkItemTableColumnKey,
} from './workItemTableColumns'

const COLUMN_PREFS_STORAGE_PREFIX = 'fleet.work-items.columns.v1'

interface StoredColumnPreferences {
    collapsedColumns?: WorkItemTableColumnKey[]
    columnWidths?: Partial<Record<WorkItemTableColumnKey, number>>
}

function getColumnPrefsStorageKey(projectId?: string): string {
    return `${COLUMN_PREFS_STORAGE_PREFIX}:${projectId ?? 'global'}`
}

function sanitizeCollapsedColumns(value: unknown): Set<WorkItemTableColumnKey> {
    if (!Array.isArray(value)) {
        return new Set()
    }

    const allowed = new Set(WORK_ITEM_TABLE_COLUMNS.map((column) => column.key))
    const next = new Set<WorkItemTableColumnKey>()
    for (const item of value) {
        if (typeof item !== 'string') {
            continue
        }

        if (allowed.has(item as WorkItemTableColumnKey)) {
            next.add(item as WorkItemTableColumnKey)
        }
    }

    return next
}

function sanitizeColumnWidths(value: unknown): Record<WorkItemTableColumnKey, number> {
    const next = { ...DEFAULT_WORK_ITEM_COLUMN_WIDTHS }
    if (!value || typeof value !== 'object') {
        return next
    }

    for (const column of WORK_ITEM_TABLE_COLUMNS) {
        const key = column.key
        const rawWidth = (value as Partial<Record<WorkItemTableColumnKey, unknown>>)[key]
        if (typeof rawWidth !== 'number' || Number.isNaN(rawWidth) || !Number.isFinite(rawWidth)) {
            continue
        }

        next[key] = Math.max(
            MIN_WORK_ITEM_COLUMN_WIDTHS[key],
            Math.round(rawWidth),
        )
    }

    return next
}

export function useWorkItemColumnPreferences(projectId: string | undefined) {
    const [collapsedColumns, setCollapsedColumns] = useState<Set<WorkItemTableColumnKey>>(() => new Set())
    const [columnWidths, setColumnWidths] = useState<Record<WorkItemTableColumnKey, number>>(
        () => ({ ...DEFAULT_WORK_ITEM_COLUMN_WIDTHS }),
    )

    useEffect(() => {
        if (typeof window === 'undefined') {
            return
        }

        try {
            const raw = window.localStorage.getItem(getColumnPrefsStorageKey(projectId))
            if (!raw) {
                setCollapsedColumns(new Set())
                setColumnWidths({ ...DEFAULT_WORK_ITEM_COLUMN_WIDTHS })
                return
            }

            const parsed = JSON.parse(raw) as StoredColumnPreferences
            setCollapsedColumns(sanitizeCollapsedColumns(parsed.collapsedColumns))
            setColumnWidths(sanitizeColumnWidths(parsed.columnWidths))
        } catch {
            setCollapsedColumns(new Set())
            setColumnWidths({ ...DEFAULT_WORK_ITEM_COLUMN_WIDTHS })
        }
    }, [projectId])

    useEffect(() => {
        if (typeof window === 'undefined') {
            return
        }

        const payload: StoredColumnPreferences = {
            collapsedColumns: Array.from(collapsedColumns),
            columnWidths,
        }

        try {
            window.localStorage.setItem(getColumnPrefsStorageKey(projectId), JSON.stringify(payload))
        } catch {
            // Ignore storage errors (for example private mode quota restrictions).
        }
    }, [collapsedColumns, columnWidths, projectId])

    const toggleColumnVisibility = useCallback((column: WorkItemTableColumnKey, visible: boolean) => {
        setCollapsedColumns((previous) => {
            const next = new Set(previous)
            if (visible) {
                next.delete(column)
            } else {
                const definition = WORK_ITEM_TABLE_COLUMNS.find((entry) => entry.key === column)
                if (definition?.collapsible) {
                    next.add(column)
                }
            }

            return next
        })
    }, [])

    const resizeColumn = useCallback((column: WorkItemTableColumnKey, width: number) => {
        setColumnWidths((previous) => {
            const nextWidth = Math.max(
                MIN_WORK_ITEM_COLUMN_WIDTHS[column],
                Math.round(width),
            )
            if (previous[column] === nextWidth) {
                return previous
            }

            return { ...previous, [column]: nextWidth }
        })
    }, [])

    const resetColumns = useCallback(() => {
        setCollapsedColumns(new Set())
        setColumnWidths({ ...DEFAULT_WORK_ITEM_COLUMN_WIDTHS })
    }, [])

    return {
        collapsedColumns,
        columnWidths,
        toggleColumnVisibility,
        resizeColumn,
        resetColumns,
    }
}
