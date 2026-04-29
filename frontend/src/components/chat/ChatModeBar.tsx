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
    BranchRegular,
    CodeRegular,
    TaskListAddRegular,
} from '@fluentui/react-icons'
import type { ChatDynamicStrategy } from '../../models'
import { usePreferences } from '../../hooks/PreferencesContext'
import { useIsMobile } from '../../hooks/useIsMobile'
import { appTokens } from '../../styles/appTokens'

interface ChatModeStrategyOption {
    value: ChatDynamicStrategy
    label: string
}

interface ChatModeBarProps {
    dynamicIterationEnabled: boolean
    dynamicBranchName: string
    dynamicStrategy: ChatDynamicStrategy
    strategyOptions: ChatModeStrategyOption[]
    policyBadges: string[]
    disabled?: boolean
    forceStackedLayout?: boolean
    onDynamicIterationChange: (enabled: boolean) => void
    onBranchNameChange: (value: string) => void
    onBranchNameCommit: () => void
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
})

export function ChatModeBar({
    dynamicIterationEnabled,
    dynamicBranchName,
    dynamicStrategy,
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
                    <Field label="Branch">
                        <Input
                            contentBefore={<BranchRegular />}
                            value={dynamicBranchName}
                            onChange={(_event, data) => onBranchNameChange(data.value)}
                            onBlur={onBranchNameCommit}
                            placeholder="main"
                            disabled={disabled}
                        />
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
