import { Button, Dropdown, Field, Input, Option, Tooltip } from '@fluentui/react-components'
import { DismissRegular, SaveRegular } from '@fluentui/react-icons'
import { resolveLevelIcon, LEVEL_ICON_NAMES } from '../../proxies'
import { appTokens } from '../../styles/appTokens'
import type { EditState } from './workItemLevelEditorTypes'

interface WorkItemLevelEditFormProps {
    editState: EditState
    setEditState: (state: EditState | null) => void
    onSave: () => void
    onCancel: () => void
    isBusy: boolean
    defaultColors: readonly string[]
    editFormClassName: string
    editFormRowClassName: string
    colorPreviewClassName: string
    editActionsClassName: string
    editActionButtonMobileClassName: string
}

export function WorkItemLevelEditForm({
    editState,
    setEditState,
    onSave,
    onCancel,
    isBusy,
    defaultColors,
    editFormClassName,
    editFormRowClassName,
    colorPreviewClassName,
    editActionsClassName,
    editActionButtonMobileClassName,
}: WorkItemLevelEditFormProps) {
    return (
        <div className={editFormClassName}>
            <Field label="Name" required>
                <Input
                    value={editState.name}
                    onChange={(_event, data) => setEditState({ ...editState, name: data.value })}
                    placeholder="Level name"
                />
            </Field>
            <div className={editFormRowClassName}>
                <Field label="Icon">
                    <Dropdown
                        value={editState.iconName}
                        onOptionSelect={(_event, data) => setEditState({ ...editState, iconName: data.optionValue ?? 'circle' })}
                    >
                        {LEVEL_ICON_NAMES.map((iconName) => (
                            <Option key={iconName} value={iconName} text={iconName}>
                                <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.375rem' }}>
                                    <span style={{ display: 'flex', alignItems: 'center', color: editState.color }}>
                                        {resolveLevelIcon(iconName)}
                                    </span>
                                    {iconName}
                                </span>
                            </Option>
                        ))}
                    </Dropdown>
                </Field>
                <Field label="Color">
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: '4px', paddingTop: '4px' }}>
                        {defaultColors.map((color) => (
                            <Tooltip key={color} content={color} relationship="label">
                                <div
                                    className={colorPreviewClassName}
                                    style={{
                                        backgroundColor: color,
                                        outline: editState.color === color ? `2px solid ${appTokens.color.brandStroke}` : undefined,
                                        outlineOffset: '1px',
                                    }}
                                    onClick={() => setEditState({ ...editState, color })}
                                />
                            </Tooltip>
                        ))}
                        <input
                            type="color"
                            value={editState.color}
                            onChange={(event) => setEditState({ ...editState, color: event.target.value })}
                            style={{ width: '32px', height: '28px', padding: 0, border: 'none', cursor: 'pointer' }}
                        />
                    </div>
                </Field>
            </div>
            <div className={editActionsClassName}>
                <Button
                    appearance="subtle"
                    icon={<DismissRegular />}
                    onClick={onCancel}
                    disabled={isBusy}
                    size="small"
                    className={editActionButtonMobileClassName}
                >
                    Cancel
                </Button>
                <Button
                    appearance="primary"
                    icon={<SaveRegular />}
                    onClick={onSave}
                    disabled={!editState.name.trim() || isBusy}
                    size="small"
                    className={editActionButtonMobileClassName}
                >
                    {editState.id === null ? 'Add' : 'Save'}
                </Button>
            </div>
        </div>
    )
}
