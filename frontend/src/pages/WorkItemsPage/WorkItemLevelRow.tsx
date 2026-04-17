import { Button, Caption1, Text, Tooltip } from '@fluentui/react-components'
import {
    ArrowDownRegular,
    ArrowUpRegular,
    DeleteRegular,
    EditRegular,
} from '@fluentui/react-icons'
import type { WorkItemLevel } from '../../models'
import { resolveLevelIcon } from '../../proxies'

interface WorkItemLevelRowProps {
    level: WorkItemLevel
    index: number
    totalCount: number
    isBusy: boolean
    levelRowClassName: string
    levelIconClassName: string
    levelPreviewClassName: string
    levelActionsClassName: string
    onMoveUp: (level: WorkItemLevel) => void
    onMoveDown: (level: WorkItemLevel) => void
    onEdit: (level: WorkItemLevel) => void
    onDelete: (levelId: number) => void
}

export function WorkItemLevelRow({
    level,
    index,
    totalCount,
    isBusy,
    levelRowClassName,
    levelIconClassName,
    levelPreviewClassName,
    levelActionsClassName,
    onMoveUp,
    onMoveDown,
    onEdit,
    onDelete,
}: WorkItemLevelRowProps) {
    return (
        <div className={levelRowClassName}>
            <span className={levelIconClassName} style={{ color: level.color }}>
                {resolveLevelIcon(level.iconName)}
            </span>
            <div className={levelPreviewClassName}>
                <Text weight="semibold" size={200}>{level.name}</Text>
                {level.isDefault ? <Caption1>(default)</Caption1> : null}
            </div>
            <div className={levelActionsClassName}>
                <Tooltip content="Move up" relationship="label">
                    <Button
                        appearance="subtle"
                        size="small"
                        icon={<ArrowUpRegular />}
                        disabled={index === 0 || isBusy}
                        onClick={() => onMoveUp(level)}
                        aria-label="Move up"
                    />
                </Tooltip>
                <Tooltip content="Move down" relationship="label">
                    <Button
                        appearance="subtle"
                        size="small"
                        icon={<ArrowDownRegular />}
                        disabled={index === totalCount - 1 || isBusy}
                        onClick={() => onMoveDown(level)}
                        aria-label="Move down"
                    />
                </Tooltip>
                <Tooltip content="Edit" relationship="label">
                    <Button
                        appearance="subtle"
                        size="small"
                        icon={<EditRegular />}
                        disabled={isBusy}
                        onClick={() => onEdit(level)}
                        aria-label="Edit"
                    />
                </Tooltip>
                <Tooltip content="Delete" relationship="label">
                    <Button
                        appearance="subtle"
                        size="small"
                        icon={<DeleteRegular />}
                        disabled={isBusy}
                        onClick={() => onDelete(level.id)}
                        aria-label="Delete"
                    />
                </Tooltip>
            </div>
        </div>
    )
}
