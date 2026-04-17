import { useCallback, useRef, type MouseEvent as ReactMouseEvent } from 'react'
import {
    MIN_WORK_ITEM_COLUMN_WIDTHS,
    type WorkItemTableColumnKey,
} from './workItemTableColumns'

interface UseResizableWorkItemColumnsOptions {
    columnWidths: Record<WorkItemTableColumnKey, number>
    onResizeColumn?: (column: WorkItemTableColumnKey, width: number) => void
}

export function useResizableWorkItemColumns({
    columnWidths,
    onResizeColumn,
}: UseResizableWorkItemColumnsOptions) {
    const activeResizeRef = useRef<{
        column: WorkItemTableColumnKey
        startX: number
        startWidth: number
    } | null>(null)

    return useCallback((event: ReactMouseEvent, column: WorkItemTableColumnKey) => {
        if (!onResizeColumn) {
            return
        }

        event.preventDefault()
        event.stopPropagation()

        const startWidth = columnWidths[column]
        activeResizeRef.current = { column, startX: event.clientX, startWidth }

        const handleMouseMove = (moveEvent: MouseEvent) => {
            const active = activeResizeRef.current
            if (!active) {
                return
            }

            const delta = moveEvent.clientX - active.startX
            const nextWidth = Math.max(
                MIN_WORK_ITEM_COLUMN_WIDTHS[active.column],
                active.startWidth + delta,
            )
            onResizeColumn(active.column, nextWidth)
        }

        const handleMouseUp = () => {
            activeResizeRef.current = null
            window.removeEventListener('mousemove', handleMouseMove)
            window.removeEventListener('mouseup', handleMouseUp)
        }

        window.addEventListener('mousemove', handleMouseMove)
        window.addEventListener('mouseup', handleMouseUp)
    }, [columnWidths, onResizeColumn])
}
