import { Caption1, Divider, Text, mergeClasses } from '@fluentui/react-components'
import { ChevronRightRegular } from '@fluentui/react-icons'
import type { WorkItem, WorkItemLevel } from '../../models'
import { resolveLevelIcon } from '../../proxies'
import { StateDot } from './StateDot'
import { formatWorkItemState } from './stateLabel'

interface WorkItemChildrenSectionProps {
    children: WorkItem[]
    levels?: WorkItemLevel[]
    isMobile: boolean
    sectionTitleClassName: string
    childRowClassName: string
    childRowMobileClassName?: string
    childIconClassName: string
    childIdClassName: string
    childTitleClassName: string
    childTitleMobileClassName?: string
    onNavigate?: (item: WorkItem) => void
}

export function WorkItemChildrenSection({
    children,
    levels,
    isMobile,
    sectionTitleClassName,
    childRowClassName,
    childRowMobileClassName,
    childIconClassName,
    childIdClassName,
    childTitleClassName,
    childTitleMobileClassName,
    onNavigate,
}: WorkItemChildrenSectionProps) {
    if (children.length === 0) {
        return null
    }

    return (
        <div>
            <Divider />
            <Text className={sectionTitleClassName} style={{ marginTop: '12px', marginBottom: '8px', display: 'block' }}>
                Child Items ({children.length})
            </Text>
            {children.map((child) => {
                const childLevel = child.levelId != null ? levels?.find((level) => level.id === child.levelId) : undefined

                return (
                    <div
                        key={child.workItemNumber}
                        className={mergeClasses(childRowClassName, isMobile && childRowMobileClassName)}
                        onClick={() => onNavigate?.(child)}
                        style={{ cursor: onNavigate ? 'pointer' : 'default' }}
                    >
                        {childLevel && (
                            <span className={childIconClassName} style={{ color: childLevel.color }}>
                                {resolveLevelIcon(childLevel.iconName)}
                            </span>
                        )}
                        <Text className={childIdClassName}>{child.workItemNumber}</Text>
                        <ChevronRightRegular fontSize={10} />
                        <Text className={mergeClasses(childTitleClassName, isMobile && childTitleMobileClassName)}>
                            {child.title}
                        </Text>
                        <StateDot state={child.state} />
                        <Caption1>{formatWorkItemState(child.state)}</Caption1>
                    </div>
                )
            })}
        </div>
    )
}
