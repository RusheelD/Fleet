import type { WorkItem } from '../../models'

export function collectDescendantWorkItemNumbers(
    items: WorkItem[],
    selectedWorkItemNumbers: Iterable<number>,
): number[] {
    const itemByNumber = new Map(items.map((item) => [item.workItemNumber, item]))
    const selectedSet = new Set(selectedWorkItemNumbers)
    const descendants = new Set<number>()
    const queue = Array.from(selectedSet)

    while (queue.length > 0) {
        const current = queue.shift()
        if (typeof current !== 'number') {
            continue
        }

        const item = itemByNumber.get(current)
        if (!item) {
            continue
        }

        for (const childNumber of item.childWorkItemNumbers) {
            if (!itemByNumber.has(childNumber) || descendants.has(childNumber) || selectedSet.has(childNumber)) {
                continue
            }

            descendants.add(childNumber)
            queue.push(childNumber)
        }
    }

    return Array.from(descendants).sort((left, right) => left - right)
}
