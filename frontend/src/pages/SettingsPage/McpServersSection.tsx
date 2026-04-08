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
import {
    useCreateMcpServer,
    useDeleteMcpServer,
    useMcpServerTemplates,
    useMcpServers,
    useSystemMcpServers,
    useUpdateMcpServer,
    useValidateMcpServer,
    getApiErrorMessage,
    type UpsertMcpServerRequest,
} from '../../proxies'
import type { McpServer, McpServerTemplate, McpServerVariable, SystemMcpServer } from '../../models'
import { useIsMobile } from '../../hooks'
import { appTokens } from '../../styles/appTokens'

type TransportType = 'stdio' | 'http'

interface DraftVariable {
    id: string
    name: string
    value: string
    isSecret: boolean
    hasValue: boolean
}

interface DraftState {
    name: string
    description: string
    transportType: TransportType
    command: string
    argumentsText: string
    workingDirectory: string
    endpoint: string
    builtInTemplateKey: string
    enabled: boolean
    environmentVariables: DraftVariable[]
    headers: DraftVariable[]
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
    templateGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
        gap: appTokens.space.md,
    },
    templateCard: {
        padding: appTokens.space.md,
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.sm,
        backgroundColor: appTokens.color.pageBackground,
    },
    serverList: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.md,
    },
    serverCard: {
        padding: appTokens.space.md,
        display: 'flex',
        justifyContent: 'space-between',
        gap: appTokens.space.md,
        alignItems: 'flex-start',
        backgroundColor: appTokens.color.pageBackground,
    },
    serverCardMobile: {
        flexDirection: 'column',
    },
    serverMeta: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xs,
        minWidth: 0,
    },
    serverBadges: {
        display: 'flex',
        gap: appTokens.space.xs,
        flexWrap: 'wrap',
    },
    serverActions: {
        display: 'flex',
        gap: appTokens.space.sm,
        flexWrap: 'wrap',
        justifyContent: 'flex-end',
    },
    serverActionsMobile: {
        width: '100%',
        justifyContent: 'stretch',
        '> .fui-Button': {
            flex: 1,
        },
    },
    form: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.md,
    },
    twoColumn: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
        gap: appTokens.space.md,
    },
    variableGroup: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.sm,
    },
    variableHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: appTokens.space.sm,
    },
    variableRow: {
        display: 'grid',
        gridTemplateColumns: '1.2fr 1.4fr auto auto',
        gap: appTokens.space.sm,
        alignItems: 'end',
    },
    variableRowMobile: {
        gridTemplateColumns: '1fr',
    },
    formActions: {
        display: 'flex',
        gap: appTokens.space.sm,
        flexWrap: 'wrap',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    statusSuccess: {
        color: appTokens.color.success,
    },
    statusError: {
        color: appTokens.color.danger,
    },
})

function createVariable(name = '', value = '', isSecret = false, hasValue = false): DraftVariable {
    return {
        id: `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
        name,
        value,
        isSecret,
        hasValue,
    }
}

function createEmptyDraft(): DraftState {
    return {
        name: '',
        description: '',
        transportType: 'stdio',
        command: '',
        argumentsText: '',
        workingDirectory: '',
        endpoint: '',
        builtInTemplateKey: '',
        enabled: true,
        environmentVariables: [],
        headers: [],
    }
}

function createDraftFromTemplate(template: McpServerTemplate): DraftState {
    return {
        name: template.name,
        description: template.description,
        transportType: template.transportType === 'http' ? 'http' : 'stdio',
        command: template.command ?? '',
        argumentsText: template.arguments.join('\n'),
        workingDirectory: template.workingDirectory ?? '',
        endpoint: template.endpoint ?? '',
        builtInTemplateKey: template.key,
        enabled: true,
        environmentVariables: template.environmentVariables.map((field) =>
            createVariable(field.name, field.defaultValue ?? '', field.isSecret, Boolean(field.defaultValue))),
        headers: template.headers.map((field) =>
            createVariable(field.name, field.defaultValue ?? '', field.isSecret, Boolean(field.defaultValue))),
    }
}

function createDraftFromServer(server: McpServer): DraftState {
    const toDraftVariable = (variable: McpServerVariable) =>
        createVariable(variable.name, variable.value ?? '', variable.isSecret, variable.hasValue)

    return {
        name: server.name,
        description: server.description ?? '',
        transportType: server.transportType === 'http' ? 'http' : 'stdio',
        command: server.command ?? '',
        argumentsText: server.arguments.join('\n'),
        workingDirectory: server.workingDirectory ?? '',
        endpoint: server.endpoint ?? '',
        builtInTemplateKey: server.builtInTemplateKey ?? '',
        enabled: server.enabled,
        environmentVariables: server.environmentVariables.map(toDraftVariable),
        headers: server.headers.map(toDraftVariable),
    }
}

function buildRequest(draft: DraftState): UpsertMcpServerRequest {
    const normalizeVariables = (variables: DraftVariable[]) =>
        variables
            .filter((variable) => variable.name.trim().length > 0)
            .map((variable) => {
                const normalizedValue = variable.value.trim()
                const preserveExistingValue = variable.isSecret && variable.hasValue && normalizedValue.length === 0

                return {
                    name: variable.name.trim(),
                    value: preserveExistingValue ? undefined : variable.value,
                    isSecret: variable.isSecret,
                    preserveExistingValue,
                }
            })

    return {
        name: draft.name.trim(),
        description: draft.description.trim(),
        transportType: draft.transportType,
        command: draft.transportType === 'stdio' ? draft.command.trim() : undefined,
        arguments: draft.transportType === 'stdio'
            ? draft.argumentsText
                .split(/\r?\n/)
                .map((line) => line.trim())
                .filter(Boolean)
            : [],
        workingDirectory: draft.transportType === 'stdio' ? draft.workingDirectory.trim() : undefined,
        endpoint: draft.transportType === 'http' ? draft.endpoint.trim() : undefined,
        builtInTemplateKey: draft.builtInTemplateKey || undefined,
        enabled: draft.enabled,
        environmentVariables: normalizeVariables(draft.environmentVariables),
        headers: normalizeVariables(draft.headers),
    }
}

function formatTimestamp(value?: string | null): string | null {
    if (!value) {
        return null
    }

    const date = new Date(value)
    if (Number.isNaN(date.getTime())) {
        return null
    }

    return date.toLocaleString()
}

export function McpServersSection() {
    const styles = useStyles()
    const isMobile = useIsMobile()
    const { data: servers = [], isLoading: isLoadingServers } = useMcpServers()
    const { data: templates = [] } = useMcpServerTemplates()
    const { data: systemServers = [] } = useSystemMcpServers()
    const createServer = useCreateMcpServer()
    const updateServer = useUpdateMcpServer()
    const deleteServer = useDeleteMcpServer()
    const validateServer = useValidateMcpServer()

    const [editingServerId, setEditingServerId] = useState<number | null>(null)
    const [draft, setDraft] = useState<DraftState>(() => createEmptyDraft())
    const [feedback, setFeedback] = useState<{ type: 'success' | 'error'; message: string } | null>(null)

    const orderedServers = useMemo(
        () => [...servers].sort((left, right) => left.name.localeCompare(right.name)),
        [servers],
    )

    const handleApplyTemplate = (template: McpServerTemplate) => {
        setEditingServerId(null)
        setDraft(createDraftFromTemplate(template))
        setFeedback(null)
    }

    const handleEditServer = (server: McpServer) => {
        setEditingServerId(server.id)
        setDraft(createDraftFromServer(server))
        setFeedback(null)
    }

    const handleReset = (clearFeedback = true) => {
        setEditingServerId(null)
        setDraft(createEmptyDraft())
        if (clearFeedback) {
            setFeedback(null)
        }
    }

    const handleSave = async () => {
        try {
            setFeedback(null)
            const request = buildRequest(draft)
            if (editingServerId) {
                await updateServer.mutateAsync({ id: editingServerId, data: request })
                handleReset(false)
                setFeedback({ type: 'success', message: 'MCP server updated.' })
            } else {
                await createServer.mutateAsync(request)
                handleReset(false)
                setFeedback({ type: 'success', message: 'MCP server created.' })
            }
        } catch (error) {
            setFeedback({ type: 'error', message: getApiErrorMessage(error, 'Unable to save MCP server.') })
        }
    }

    const handleDelete = async (id: number) => {
        try {
            setFeedback(null)
            await deleteServer.mutateAsync(id)
            if (editingServerId === id) {
                handleReset()
            }
            setFeedback({ type: 'success', message: 'MCP server deleted.' })
        } catch (error) {
            setFeedback({ type: 'error', message: getApiErrorMessage(error, 'Unable to delete MCP server.') })
        }
    }

    const handleValidate = async (id: number) => {
        try {
            setFeedback(null)
            const result = await validateServer.mutateAsync(id)
            setFeedback({
                type: result.success ? 'success' : 'error',
                message: result.success
                    ? `Validation succeeded. ${result.toolCount} tool${result.toolCount === 1 ? '' : 's'} discovered.`
                    : (result.error ?? 'Validation failed.'),
            })
        } catch (error) {
            setFeedback({ type: 'error', message: getApiErrorMessage(error, 'Unable to validate MCP server.') })
        }
    }

    const updateDraft = (patch: Partial<DraftState>) => {
        setDraft((current) => ({ ...current, ...patch }))
    }

    const updateVariable = (key: 'environmentVariables' | 'headers', id: string, patch: Partial<DraftVariable>) => {
        setDraft((current) => ({
            ...current,
            [key]: current[key].map((variable) =>
                variable.id === id
                    ? { ...variable, ...patch, hasValue: patch.value !== undefined ? variable.hasValue || patch.value.length > 0 : variable.hasValue }
                    : variable,
            ),
        }))
    }

    const addVariable = (key: 'environmentVariables' | 'headers') => {
        setDraft((current) => ({
            ...current,
            [key]: [...current[key], createVariable()],
        }))
    }

    const removeVariable = (key: 'environmentVariables' | 'headers', id: string) => {
        setDraft((current) => ({
            ...current,
            [key]: current[key].filter((variable) => variable.id !== id),
        }))
    }

    const renderVariableEditor = (title: string, key: 'environmentVariables' | 'headers') => (
        <div className={styles.variableGroup}>
            <div className={styles.variableHeader}>
                <Text weight="semibold">{title}</Text>
                <Button appearance="subtle" size="small" onClick={() => addVariable(key)}>
                    Add
                </Button>
            </div>
            {draft[key].length === 0 && <Caption1>No {title.toLowerCase()} configured.</Caption1>}
            {draft[key].map((variable) => (
                <div
                    key={variable.id}
                    className={mergeClasses(styles.variableRow, isMobile && styles.variableRowMobile)}
                >
                    <Field label="Name">
                        <Input
                            value={variable.name}
                            onChange={(_event, data) => updateVariable(key, variable.id, { name: data.value })}
                        />
                    </Field>
                    <Field
                        label={variable.isSecret ? 'Secret Value' : 'Value'}
                        hint={
                            variable.isSecret && variable.hasValue && variable.value.length === 0
                                ? 'Leave blank to keep the stored secret.'
                                : undefined
                        }
                    >
                        <Input
                            type={variable.isSecret ? 'password' : 'text'}
                            value={variable.value}
                            placeholder={variable.isSecret && variable.hasValue && variable.value.length === 0 ? 'Stored secret' : ''}
                            onChange={(_event, data) => updateVariable(key, variable.id, { value: data.value })}
                        />
                    </Field>
                    <Field label="Secret">
                        <Switch
                            checked={variable.isSecret}
                            onChange={(_event, data) => updateVariable(key, variable.id, { isSecret: data.checked })}
                        />
                    </Field>
                    <Button appearance="subtle" size="small" onClick={() => removeVariable(key, variable.id)}>
                        Remove
                    </Button>
                </div>
            ))}
        </div>
    )

    return (
        <Card className={mergeClasses(styles.section, isMobile && styles.sectionMobile)}>
            <Title3>MCP Servers</Title3>
            <Caption1>
                Add built-in servers like Playwright, or connect your own MCP servers over stdio or HTTP for GitHub,
                Azure DevOps, docs, QA tools, and internal systems.
            </Caption1>
            <Divider />

            <div className={styles.templateGrid}>
                {templates.map((template) => (
                    <Card key={template.key} className={styles.templateCard}>
                        <Text weight="semibold">{template.name}</Text>
                        <Caption1>{template.description}</Caption1>
                        <Caption1>{template.notes[0]}</Caption1>
                        <Button appearance="secondary" size="small" onClick={() => handleApplyTemplate(template)}>
                            Use Template
                        </Button>
                    </Card>
                ))}
            </div>

            <Divider />

            <div className={styles.form}>
                <Text weight="semibold">{editingServerId ? 'Edit MCP Server' : 'New MCP Server'}</Text>
                <div className={styles.twoColumn}>
                    <Field label="Name" required>
                        <Input value={draft.name} onChange={(_event, data) => updateDraft({ name: data.value })} />
                    </Field>
                    <Field label="Transport" required>
                        <Dropdown
                            selectedOptions={[draft.transportType]}
                            value={draft.transportType === 'http' ? 'HTTP' : 'Stdio'}
                            onOptionSelect={(_event, data) => updateDraft({ transportType: (data.optionValue as TransportType | undefined) ?? 'stdio' })}
                        >
                            <Option value="stdio">Stdio</Option>
                            <Option value="http">HTTP</Option>
                        </Dropdown>
                    </Field>
                </div>

                <Field label="Description">
                    <Textarea
                        value={draft.description}
                        onChange={(_event, data) => updateDraft({ description: data.value })}
                        resize="vertical"
                    />
                </Field>

                {draft.transportType === 'stdio' ? (
                    <>
                        <div className={styles.twoColumn}>
                            <Field label="Command" required>
                                <Input value={draft.command} onChange={(_event, data) => updateDraft({ command: data.value })} />
                            </Field>
                            <Field label="Working Directory">
                                <Input value={draft.workingDirectory} onChange={(_event, data) => updateDraft({ workingDirectory: data.value })} />
                            </Field>
                        </div>
                        <Field label="Arguments" hint="One argument per line.">
                            <Textarea
                                value={draft.argumentsText}
                                onChange={(_event, data) => updateDraft({ argumentsText: data.value })}
                                resize="vertical"
                            />
                        </Field>
                    </>
                ) : (
                    <Field label="Endpoint" required hint="Absolute http or https URL.">
                        <Input value={draft.endpoint} onChange={(_event, data) => updateDraft({ endpoint: data.value })} />
                    </Field>
                )}

                <Field label="Enabled">
                    <Switch checked={draft.enabled} onChange={(_event, data) => updateDraft({ enabled: data.checked })} />
                </Field>

                <div className={styles.twoColumn}>
                    {renderVariableEditor('Environment Variables', 'environmentVariables')}
                    {renderVariableEditor('Headers', 'headers')}
                </div>

                <div className={styles.formActions}>
                    <div>
                        {feedback && (
                            <Caption1 className={feedback.type === 'success' ? styles.statusSuccess : styles.statusError}>
                                {feedback.message}
                            </Caption1>
                        )}
                    </div>
                    <div className={styles.serverActions}>
                        <Button appearance="subtle" onClick={() => handleReset()}>
                            Clear
                        </Button>
                        <Button
                            appearance="primary"
                            onClick={handleSave}
                            disabled={createServer.isPending || updateServer.isPending}
                        >
                            {editingServerId ? 'Save Changes' : 'Create Server'}
                        </Button>
                    </div>
                </div>
            </div>

            <Divider />

            <div className={styles.serverList}>
                <Text weight="semibold">Configured Servers</Text>
                {isLoadingServers && <Caption1>Loading MCP servers…</Caption1>}
                {!isLoadingServers && orderedServers.length === 0 && (
                    <Caption1>No MCP servers configured yet.</Caption1>
                )}
                {orderedServers.map((server) => {
                    const validatedAt = formatTimestamp(server.lastValidatedAtUtc)
                    return (
                        <Card
                            key={server.id}
                            className={mergeClasses(styles.serverCard, isMobile && styles.serverCardMobile)}
                        >
                            <div className={styles.serverMeta}>
                                <Text weight="semibold">{server.name}</Text>
                                <Caption1>{server.description || (server.transportType === 'http' ? server.endpoint : server.command) || 'No description provided.'}</Caption1>
                                <div className={styles.serverBadges}>
                                    <Badge appearance="outline">{server.transportType.toUpperCase()}</Badge>
                                    <Badge appearance="filled" color={server.enabled ? 'success' : 'subtle'}>
                                        {server.enabled ? 'Enabled' : 'Disabled'}
                                    </Badge>
                                    {server.builtInTemplateKey && <Badge appearance="tint">Template</Badge>}
                                </div>
                                {validatedAt && (
                                    <Caption1>
                                        Last validated: {validatedAt}
                                    </Caption1>
                                )}
                                {server.lastValidationError && (
                                    <Caption1 className={styles.statusError}>{server.lastValidationError}</Caption1>
                                )}
                                {!server.lastValidationError && server.lastToolCount > 0 && (
                                    <Caption1>
                                        {server.lastToolCount} tool{server.lastToolCount === 1 ? '' : 's'} discovered
                                        {server.discoveredTools.length > 0 ? `: ${server.discoveredTools.join(', ')}` : '.'}
                                    </Caption1>
                                )}
                            </div>
                            <div className={mergeClasses(styles.serverActions, isMobile && styles.serverActionsMobile)}>
                                <Button appearance="secondary" size="small" onClick={() => handleEditServer(server)}>
                                    Edit
                                </Button>
                                <Button
                                    appearance="secondary"
                                    size="small"
                                    onClick={() => handleValidate(server.id)}
                                    disabled={validateServer.isPending}
                                >
                                    Validate
                                </Button>
                                <Button
                                    appearance="subtle"
                                    size="small"
                                    onClick={() => handleDelete(server.id)}
                                    disabled={deleteServer.isPending}
                                >
                                    Delete
                                </Button>
                            </div>
                        </Card>
                    )
                })}
            </div>

            {systemServers.length > 0 && (
                <>
                    <Divider />
                    <div className={styles.serverList}>
                        <Text weight="semibold">System Servers</Text>
                        <Caption1>
                            These MCP servers are configured at the system level and are automatically available to all users and agents.
                        </Caption1>
                        {systemServers.map((server: SystemMcpServer) => (
                            <Card
                                key={server.name}
                                className={mergeClasses(styles.serverCard, isMobile && styles.serverCardMobile)}
                            >
                                <div className={styles.serverMeta}>
                                    <Text weight="semibold">{server.name}</Text>
                                    <Caption1>{server.transportType === 'http' ? server.endpoint : `${server.command ?? ''} ${server.arguments.join(' ')}`}</Caption1>
                                    <div className={styles.serverBadges}>
                                        <Badge appearance="outline">{server.transportType.toUpperCase()}</Badge>
                                        <Badge appearance="filled" color="brand">System</Badge>
                                    </div>
                                </div>
                            </Card>
                        ))}
                    </div>
                </>
            )}
        </Card>
    )
}
