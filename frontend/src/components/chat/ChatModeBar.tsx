import { useEffect, useMemo, useState } from 'react'
import {
    Badge,
    Dropdown,
    Field,
    Input,
    Option,
    Switch,
    makeStyles,
    mergeClasses,
} from '@fluentui/react-components'
import {
    AddRegular,
    BranchRegular,
    CodeRegular,
    LockClosedRegular,
    TaskListAddRegular,
} from '@fluentui/react-icons'
import type { ChatDynamicStrategy, ProjectBranch } from '../../models'
import { usePreferences } from '../../hooks/PreferencesContext'
import { useIsMobile } from '../../hooks/useIsMobile'
import { appTokens } from '../../styles/appTokens'

const CREATE_NEW_BRANCH_OPTION = '__fleet_create_new_branch__'

interface ChatModeStrategyOption {
    value: ChatDynamicStrategy
    label: string
}

interface ChatModeBarProps {
    dynamicIterationEnabled: boolean
    dynamicBranchName: string
    dynamicStrategy: ChatDynamicStrategy
    branchOptions: ProjectBranch[]
    branchesLoading?: boolean
    branchValidationMessage?: string | null
    branchValidationState?: 'error' | 'warning'
    strategyOptions: ChatModeStrategyOption[]
    policyBadges: string[]
    disabled?: boolean
    forceStackedLayout?: boolean
    onDynamicIterationChange: (enabled: boolean) => void
    onBranchNameChange: (value: string) => void
    onBranchNameCommit: (value?: string) => void
    onStrategyChange: (strategy: ChatDynamicStrategy) => void
}

const useStyles = makeStyles({
    modeBar: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.sm,
        paddingTop: appTokens.space.sm,
        paddingBottom: appTokens.space.sm,
        paddingLeft: appTokens.space.md,
        paddingRight: appTokens.space.md,
        borderTop: appTokens.border.subtle,
        backgroundColor: appTokens.color.surfaceAlt,
        flexShrink: 0,
    },
    modeBarCompact: {
        gap: appTokens.space.xs,
        paddingTop: appTokens.space.xs,
        paddingBottom: appTokens.space.xs,
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
    },
    modeHeader: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: appTokens.space.sm,
        minWidth: 0,
    },
    modeHeaderStacked: {
        alignItems: 'stretch',
        flexDirection: 'column',
    },
    modeSummary: {
        display: 'flex',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: appTokens.space.xs,
        minWidth: 0,
    },
    modeBadge: {
        maxWidth: '100%',
    },
    modeSwitch: {
        flexShrink: 0,
    },
    controlsGrid: {
        display: 'grid',
        gridTemplateColumns: 'minmax(0, 1fr) minmax(10rem, 13rem)',
        gap: appTokens.space.sm,
    },
    controlsGridStacked: {
        gridTemplateColumns: '1fr',
    },
    branchCreateInput: {
        marginTop: appTokens.space.xs,
    },
    branchOption: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.xs,
        minWidth: 0,
    },
    branchOptionName: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
})

export function ChatModeBar({
    dynamicIterationEnabled,
    dynamicBranchName,
    dynamicStrategy,
    branchOptions,
    branchesLoading,
    branchValidationMessage,
    branchValidationState,
    strategyOptions,
    policyBadges,
    disabled,
    forceStackedLayout = false,
    onDynamicIterationChange,
    onBranchNameChange,
    onBranchNameCommit,
    onStrategyChange,
}: ChatModeBarProps) {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isMobile = useIsMobile()
    const isCompact = preferences?.compactMode ?? false
    const shouldStackLayout = isMobile || forceStackedLayout
    const selectedStrategyLabel = strategyOptions.find((option) => option.value === dynamicStrategy)?.label ?? 'Balanced'
    const normalizedBranchName = dynamicBranchName.trim()
    const selectedBranch = useMemo(
        () => branchOptions.find((branch) => stringEqualsIgnoreCase(branch.name, normalizedBranchName)),
        [branchOptions, normalizedBranchName],
    )
    const [isCreatingBranch, setIsCreatingBranch] = useState(false)

    useEffect(() => {
        if (!dynamicIterationEnabled) {
            setIsCreatingBranch(false)
            return
        }

        if (branchesLoading) {
            return
        }

        if (normalizedBranchName.length > 0 && !selectedBranch) {
            setIsCreatingBranch(true)
        }
    }, [branchesLoading, dynamicIterationEnabled, normalizedBranchName, selectedBranch])

    const branchDropdownValue = isCreatingBranch
        ? 'Create new branch'
        : selectedBranch
            ? formatBranchLabel(selectedBranch)
            : branchesLoading
                ? 'Loading branches...'
                : 'Repository default'
    const selectedBranchOptions = isCreatingBranch
        ? [CREATE_NEW_BRANCH_OPTION]
        : selectedBranch
            ? [selectedBranch.name]
            : []

    return (
        <div className={mergeClasses(styles.modeBar, isCompact && styles.modeBarCompact)}>
            <div className={mergeClasses(styles.modeHeader, shouldStackLayout && styles.modeHeaderStacked)}>
                <div className={styles.modeSummary}>
                    <Badge
                        appearance="tint"
                        icon={dynamicIterationEnabled ? <CodeRegular /> : <TaskListAddRegular />}
                        className={styles.modeBadge}
                    >
                        {dynamicIterationEnabled ? 'Iteration mode' : 'Planning mode'}
                    </Badge>
                    {policyBadges.map((badge) => (
                        <Badge key={badge} appearance="tint">
                            {badge}
                        </Badge>
                    ))}
                </div>
                <Switch
                    className={styles.modeSwitch}
                    label="Dynamic iteration"
                    checked={dynamicIterationEnabled}
                    onChange={(_event, data) => onDynamicIterationChange(Boolean(data.checked))}
                    disabled={disabled}
                />
            </div>

            {dynamicIterationEnabled && (
                <div className={mergeClasses(styles.controlsGrid, shouldStackLayout && styles.controlsGridStacked)}>
                    <Field
                        label="Target branch"
                        validationMessage={branchValidationMessage ?? undefined}
                        validationState={branchValidationState}
                    >
                        <Dropdown
                            value={branchDropdownValue}
                            selectedOptions={selectedBranchOptions}
                            onOptionSelect={(_event, data) => {
                                const selectedValue = data.optionValue
                                if (selectedValue === CREATE_NEW_BRANCH_OPTION) {
                                    setIsCreatingBranch(true)
                                    if (selectedBranch) {
                                        onBranchNameChange('')
                                        onBranchNameCommit('')
                                    }
                                    return
                                }

                                if (selectedValue) {
                                    setIsCreatingBranch(false)
                                    onBranchNameChange(selectedValue)
                                    onBranchNameCommit(selectedValue)
                                }
                            }}
                            disabled={disabled}
                        >
                            <Option value={CREATE_NEW_BRANCH_OPTION} text="Create new branch">
                                <span className={styles.branchOption}>
                                    <AddRegular />
                                    <span>Create new branch</span>
                                </span>
                            </Option>
                            {branchOptions.map((branch) => (
                                <Option
                                    key={branch.name}
                                    value={branch.name}
                                    text={formatBranchLabel(branch)}
                                    disabled={!branch.canUseForDynamicIteration}
                                >
                                    <span className={styles.branchOption}>
                                        {branch.isProtected ? <LockClosedRegular /> : <BranchRegular />}
                                        <span className={styles.branchOptionName}>{formatBranchLabel(branch)}</span>
                                    </span>
                                </Option>
                            ))}
                        </Dropdown>
                        {isCreatingBranch && (
                            <Input
                                className={styles.branchCreateInput}
                                contentBefore={<BranchRegular />}
                                value={dynamicBranchName}
                                onChange={(_event, data) => onBranchNameChange(data.value)}
                                onBlur={() => onBranchNameCommit(dynamicBranchName)}
                                placeholder="feature/dynamic-iteration"
                                disabled={disabled}
                            />
                        )}
                    </Field>
                    <Field label="Strategy">
                        <Dropdown
                            value={selectedStrategyLabel}
                            selectedOptions={[dynamicStrategy]}
                            onOptionSelect={(_event, data) => {
                                const selected = data.optionValue
                                if (selected === 'balanced' || selected === 'parallel' || selected === 'sequential') {
                                    onStrategyChange(selected)
                                }
                            }}
                            disabled={disabled}
                        >
                            {strategyOptions.map((option) => (
                                <Option key={option.value} value={option.value}>
                                    {option.label}
                                </Option>
                            ))}
                        </Dropdown>
                    </Field>
                </div>
            )}
        </div>
    )
}

function formatBranchLabel(branch: ProjectBranch): string {
    const suffixes = [
        branch.isDefault ? 'default' : null,
        branch.isProtected ? 'protected' : null,
    ].filter(Boolean)

    return suffixes.length > 0
        ? `${branch.name} (${suffixes.join(', ')})`
        : branch.name
}

function stringEqualsIgnoreCase(left: string, right: string): boolean {
    return left.localeCompare(right, undefined, { sensitivity: 'accent' }) === 0
}
