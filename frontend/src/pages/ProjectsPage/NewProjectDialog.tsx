import { useEffect, useMemo, useState, type SyntheticEvent } from 'react'
import {
    makeStyles,
    mergeClasses,
    Button,
    Input,
    Textarea,
    Field,
    Dialog,
    DialogSurface,
    DialogTitle,
    DialogBody,
    DialogContent,
    DialogActions,
    tokens,
    Text,
    Spinner,
    Combobox,
    Dropdown,
    Option,
    Caption1,
    Radio,
    RadioGroup,
    Checkbox,
} from '@fluentui/react-components'
import { CheckmarkCircle16Filled, DismissCircle16Filled, LockClosedRegular } from '@fluentui/react-icons'
import { getApiErrorMessage, useCreateGitHubRepo, useCreateProject, useCheckSlug, useGitHubRepos, useUserSettings } from '../../proxies'
import { useIsMobile } from '../../hooks'
import { canCreateProject } from './projectCreationGate'

function generateSlugPreview(name: string): string {
    return name
        .toLowerCase()
        .trim()
        .replace(/[^a-z0-9\s-]/g, '')
        .replace(/[\s]+/g, '-')
        .replace(/-+/g, '-')
        .replace(/^-|-$/g, '')
}

function isValidGitHubRepoName(name: string): boolean {
    return /^[A-Za-z0-9._-]+$/.test(name)
}

const useStyles = makeStyles({
    dialogSurface: {
        width: 'min(680px, calc(100vw - 2rem))',
    },
    dialogSurfaceMobile: {
        width: 'calc(100vw - 0.75rem)',
        maxHeight: 'calc(100vh - 0.75rem)',
    },
    dialogForm: {
        display: 'flex',
        flexDirection: 'column',
        gap: '1.125rem',
    },
    dialogFormMobile: {
        gap: '0.875rem',
    },
    repoModeGroup: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
    sectionStack: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    repoHint: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
    },
    slugRow: {
        display: 'flex',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalXS,
        marginTop: tokens.spacingVerticalXS,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
        paddingTop: '2px',
        paddingBottom: '2px',
        paddingLeft: '6px',
        paddingRight: '6px',
        width: '100%',
    },
    slugText: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
        fontFamily: tokens.fontFamilyMonospace,
    },
    slugAvailable: {
        color: tokens.colorPaletteGreenForeground1,
    },
    slugUnavailable: {
        color: tokens.colorPaletteRedForeground1,
    },
    repoAvailabilityRow: {
        display: 'flex',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalXS,
        marginTop: tokens.spacingVerticalXS,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
        paddingTop: '2px',
        paddingBottom: '2px',
        paddingLeft: '6px',
        paddingRight: '6px',
        width: '100%',
    },
    repoAvailabilityText: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
        fontFamily: tokens.fontFamilyMonospace,
    },
    repoAvailabilityAvailable: {
        color: tokens.colorPaletteGreenForeground1,
    },
    repoAvailabilityUnavailable: {
        color: tokens.colorPaletteRedForeground1,
    },
    repoOption: {
        display: 'flex',
        flexDirection: 'column',
        gap: '2px',
    },
    repoName: {
        fontFamily: tokens.fontFamilyMonospace,
        fontSize: tokens.fontSizeBase200,
    },
    repoDescription: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase100,
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        maxWidth: '320px',
    },
    repoDescriptionMobile: {
        maxWidth: 'unset',
        whiteSpace: 'normal',
    },
    privateIcon: {
        color: tokens.colorNeutralForeground3,
        fontSize: '12px',
        marginLeft: '4px',
    },
    repoCombobox: {
        width: '100%',
    },
    accountOptionMeta: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase100,
    },
    formError: {
        color: tokens.colorPaletteRedForeground1,
    },
    privacyToggle: {
        marginTop: tokens.spacingVerticalXXS,
    },
    dialogActionsMobile: {
        width: '100%',
        justifyContent: 'stretch',
        display: 'grid',
        gap: tokens.spacingVerticalXS,
    },
    dialogActionButtonMobile: {
        width: '100%',
    },
})

interface NewProjectDialogProps {
    open: boolean
    onOpenChange: (open: boolean) => void
    onCreated?: (slug: string) => void
}

export function NewProjectDialog({ open, onOpenChange, onCreated }: NewProjectDialogProps) {
    const styles = useStyles()
    const isMobile = useIsMobile()
    const createMutation = useCreateProject()
    const createRepoMutation = useCreateGitHubRepo()

    const [title, setTitle] = useState('')
    const [description, setDescription] = useState('')
    const [repoMode, setRepoMode] = useState<'existing' | 'new'>('existing')
    const [repo, setRepo] = useState('')
    const [repoSearch, setRepoSearch] = useState('')
    const [newRepoName, setNewRepoName] = useState('')
    const [newRepoDescription, setNewRepoDescription] = useState('')
    const [newRepoPrivate, setNewRepoPrivate] = useState(false)
    const [selectedAccountId, setSelectedAccountId] = useState<number | undefined>(undefined)
    const [submissionError, setSubmissionError] = useState<string | null>(null)

    const { data: settings } = useUserSettings()
    const gitHubConnections = useMemo(
        () => (settings?.connections ?? [])
            .filter((connection) => connection.provider === 'GitHub' && connection.connectedAs)
            .sort((a, b) => {
                if (!!a.isPrimary !== !!b.isPrimary) {
                    return a.isPrimary ? -1 : 1
                }
                return (b.connectedAt ?? '').localeCompare(a.connectedAt ?? '')
            }),
        [settings?.connections],
    )

    const hasGitHub = gitHubConnections.length > 0

    useEffect(() => {
        if (!open || gitHubConnections.length === 0) {
            return
        }

        setSelectedAccountId((current) => {
            if (typeof current === 'number' && gitHubConnections.some((account) => account.id === current)) {
                return current
            }

            return gitHubConnections.find((account) => account.isPrimary)?.id ?? gitHubConnections[0].id
        })
    }, [open, gitHubConnections])

    const selectedAccount = useMemo(
        () => gitHubConnections.find((account) => account.id === selectedAccountId),
        [gitHubConnections, selectedAccountId],
    )

    const {
        data: repos,
        isLoading: isLoadingRepos,
        error: reposError,
    } = useGitHubRepos(hasGitHub && open && repoMode === 'existing')
    const {
        data: selectedAccountRepos,
        isLoading: isLoadingSelectedAccountRepos,
        error: selectedAccountReposError,
    } = useGitHubRepos(
        hasGitHub && open && repoMode === 'new' && typeof selectedAccountId === 'number',
        selectedAccountId,
    )

    const filteredRepos = useMemo(() => {
        if (!repos) return []
        if (!repoSearch) return repos

        const q = repoSearch.toLowerCase()
        return repos
            .map((item) => {
                const name = item.fullName.toLowerCase()
                const desc = item.description?.toLowerCase() ?? ''
                const nameIdx = name.indexOf(q)
                const descIdx = desc.indexOf(q)
                if (nameIdx === -1 && descIdx === -1) return null

                const score = nameIdx !== -1 ? nameIdx : 1000 + descIdx
                return { repo: item, score }
            })
            .filter((item): item is { repo: typeof repos[number]; score: number } => item !== null)
            .sort((a, b) => a.score - b.score)
            .map((item) => item.repo)
    }, [repos, repoSearch])

    const slugPreview = useMemo(() => generateSlugPreview(title), [title])
    const slugCheck = useCheckSlug(slugPreview)

    const slugAvailable = slugCheck.data?.available === true
    const slugUnavailable = slugCheck.data?.available === false && slugPreview.length > 0
    const isCheckingSlug = slugCheck.isLoading && slugPreview.length > 0

    const normalizedNewRepoName = newRepoName.trim()
    const hasSelectedRepo = repos?.some((item) => item.fullName === repo.trim()) ?? false
    const hasSelectedAccount = typeof selectedAccountId === 'number'
        && gitHubConnections.some((account) => account.id === selectedAccountId)
    const selectedAccountLogin = selectedAccount?.connectedAs?.trim() ?? ''
    const existingRepoErrorMessage = reposError
        ? getApiErrorMessage(reposError, 'Unable to load GitHub repositories.')
        : null
    const selectedAccountRepoErrorMessage = selectedAccountReposError
        ? getApiErrorMessage(selectedAccountReposError, 'Unable to verify repository access for this GitHub account.')
        : null
    const newRepoNameValid = normalizedNewRepoName.length > 0 && isValidGitHubRepoName(normalizedNewRepoName)
    const canCheckNewRepoAvailability =
        repoMode === 'new'
        && hasSelectedAccount
        && selectedAccountLogin.length > 0
        && normalizedNewRepoName.length > 0
        && newRepoNameValid
    const isCheckingNewRepoAvailability = canCheckNewRepoAvailability && isLoadingSelectedAccountRepos
    const isNewRepoNameTaken = useMemo(() => {
        if (!canCheckNewRepoAvailability || !selectedAccountRepos) {
            return false
        }

        const candidateName = normalizedNewRepoName.toLowerCase()
        return selectedAccountRepos.some((item) => item.name.toLowerCase() === candidateName)
    }, [canCheckNewRepoAvailability, selectedAccountRepos, normalizedNewRepoName])
    const isPending = createMutation.isPending || createRepoMutation.isPending

    const canCreate = canCreateProject({
        title,
        slugPreview,
        slugAvailable,
        hasGitHub,
        repoMode,
        hasSelectedRepo,
        hasSelectedAccount,
        newRepoNameValid,
        newRepoNameTaken: isNewRepoNameTaken,
        hasGitHubRepoError: repoMode === 'existing'
            ? !!existingRepoErrorMessage
            : !!selectedAccountRepoErrorMessage,
        isCheckingGitHubRepoState: repoMode === 'existing'
            ? isLoadingRepos
            : isCheckingNewRepoAvailability,
        isPending,
    })

    const resetForm = () => {
        setTitle('')
        setDescription('')
        setRepoMode('existing')
        setRepo('')
        setRepoSearch('')
        setNewRepoName('')
        setNewRepoDescription('')
        setNewRepoPrivate(false)
        setSelectedAccountId(undefined)
        setSubmissionError(null)
    }

    const handleCreate = async () => {
        if (!canCreate) return

        setSubmissionError(null)
        let repositoryFullName = repo.trim()

        if (repoMode === 'new') {
            try {
                const createdRepo = await createRepoMutation.mutateAsync({
                    name: normalizedNewRepoName,
                    description: newRepoDescription.trim() || undefined,
                    private: newRepoPrivate,
                    accountId: selectedAccountId,
                })
                repositoryFullName = createdRepo.fullName
            } catch (error) {
                setSubmissionError(getApiErrorMessage(error, 'Unable to create the GitHub repository.'))
                return
            }
        }

        try {
            const project = await createMutation.mutateAsync({
                title: title.trim(),
                description: description.trim(),
                repo: repositoryFullName,
            })

            resetForm()
            onOpenChange(false)
            onCreated?.(project.slug)
        } catch (error) {
            setSubmissionError(getApiErrorMessage(
                error,
                repoMode === 'new'
                    ? 'The GitHub repository was created, but Fleet could not create the project.'
                    : 'Unable to create the project.',
            ))
        }
    }

    return (
        <Dialog open={open} onOpenChange={(_e, data) => { onOpenChange(data.open); if (!data.open) resetForm() }}>
            <DialogSurface className={mergeClasses(styles.dialogSurface, isMobile && styles.dialogSurfaceMobile)}>
                <DialogBody>
                    <DialogTitle>Create New Project</DialogTitle>
                    <DialogContent>
                        <div className={mergeClasses(styles.dialogForm, isMobile && styles.dialogFormMobile)}>
                            <Field
                                label="Project Title"
                                required
                                validationState={slugUnavailable ? 'error' : undefined}
                                validationMessage={slugUnavailable ? 'A project with this name already exists' : undefined}
                            >
                                <Input
                                    placeholder="My awesome product"
                                    value={title}
                                    onChange={(_e, data) => setTitle(data.value)}
                                />
                                {slugPreview.length > 0 && (
                                    <div className={styles.slugRow}>
                                        {isCheckingSlug ? (
                                            <Spinner size="extra-tiny" />
                                        ) : slugAvailable ? (
                                            <CheckmarkCircle16Filled className={styles.slugAvailable} />
                                        ) : slugUnavailable ? (
                                            <DismissCircle16Filled className={styles.slugUnavailable} />
                                        ) : null}
                                        <Text className={styles.slugText}>
                                            {slugPreview}
                                        </Text>
                                    </div>
                                )}
                            </Field>

                            <Field label="Description">
                                <Textarea
                                    placeholder="Describe what this project is about..."
                                    resize="vertical"
                                    rows={3}
                                    value={description}
                                    onChange={(_e, data) => setDescription(data.value)}
                                />
                            </Field>

                            <Field label="Repository Source" required>
                                <RadioGroup
                                    className={styles.repoModeGroup}
                                    value={repoMode}
                                    onChange={(_e, data) => setRepoMode(data.value as 'existing' | 'new')}
                                >
                                    <Radio value="existing" label="Use an existing repository" />
                                    <Radio value="new" label="Create a new repository" />
                                </RadioGroup>
                            </Field>

                            {repoMode === 'existing' ? (
                                <Field
                                    label="GitHub Repository"
                                    required
                                    validationState={existingRepoErrorMessage || (repo.trim().length > 0 && !hasSelectedRepo) ? 'error' : undefined}
                                    validationMessage={existingRepoErrorMessage
                                        ?? (repo.trim().length > 0 && !hasSelectedRepo
                                            ? 'Select a repository from your linked GitHub list.'
                                            : undefined)}
                                    hint={
                                        hasGitHub ? undefined : (
                                            <Text className={styles.repoHint}>
                                                Link your GitHub account in Settings {'>'} Connections to create a project
                                            </Text>
                                        )
                                    }
                                >
                                    {hasGitHub ? (
                                        <Combobox
                                            className={styles.repoCombobox}
                                            placeholder={existingRepoErrorMessage
                                                ? 'Reconnect GitHub to load repositories'
                                                : (isLoadingRepos ? 'Loading repositories...' : 'Search your repositories...')}
                                            value={repo || repoSearch}
                                            freeform
                                            onInput={(event: SyntheticEvent<HTMLInputElement>) => {
                                                const newValue = event.currentTarget.value
                                                setRepoSearch(newValue)
                                                if (!repos?.some((item) => item.fullName === newValue)) {
                                                    setRepo('')
                                                }
                                            }}
                                            onOptionSelect={(_event: SyntheticEvent<HTMLElement>, data: { optionValue?: string }) => {
                                                if (data.optionValue) {
                                                    setRepo(data.optionValue)
                                                    setRepoSearch('')
                                                }
                                            }}
                                        >
                                            {isLoadingRepos ? (
                                                <Option disabled key="__loading" text="">
                                                    <Spinner size="tiny" label="Loading repos..." />
                                                </Option>
                                            ) : existingRepoErrorMessage ? (
                                                <Option key="__error" disabled text="">
                                                    {existingRepoErrorMessage}
                                                </Option>
                                            ) : filteredRepos.length > 0 ? (
                                                filteredRepos.map((item) => (
                                                    <Option key={item.fullName} value={item.fullName} text={item.fullName}>
                                                        <div className={styles.repoOption}>
                                                            <Text className={styles.repoName}>
                                                                {item.fullName}
                                                                {item.private && <LockClosedRegular className={styles.privateIcon} />}
                                                            </Text>
                                                            {item.linkedAccountLogin && (
                                                                <Caption1 className={styles.accountOptionMeta}>
                                                                    via {item.linkedAccountLogin}
                                                                </Caption1>
                                                            )}
                                                            {item.description && (
                                                                <Caption1 className={mergeClasses(styles.repoDescription, isMobile && styles.repoDescriptionMobile)}>
                                                                    {item.description}
                                                                </Caption1>
                                                            )}
                                                        </div>
                                                    </Option>
                                                ))
                                            ) : repoSearch ? (
                                                <Option key="__no-match" disabled text="">
                                                    No repositories match "{repoSearch}"
                                                </Option>
                                            ) : (
                                                <Option key="__empty" disabled text="">
                                                    No repositories found
                                                </Option>
                                            )}
                                        </Combobox>
                                    ) : null}
                                </Field>
                            ) : (
                                <div className={styles.sectionStack}>
                                    <Field
                                        label="GitHub Account"
                                        required
                                        validationState={selectedAccountRepoErrorMessage || (hasGitHub && !hasSelectedAccount) ? 'error' : undefined}
                                        validationMessage={selectedAccountRepoErrorMessage
                                            ?? (hasGitHub && !hasSelectedAccount ? 'Select which GitHub account to use.' : undefined)}
                                        hint={
                                            hasGitHub ? 'Primary account is selected by default.' : (
                                                <Text className={styles.repoHint}>
                                                    Link your GitHub account in Settings {'>'} Connections to create a repository
                                                </Text>
                                            )
                                        }
                                    >
                                        {hasGitHub ? (
                                            <Dropdown
                                                placeholder="Select GitHub account"
                                                selectedOptions={selectedAccount ? [selectedAccount.id.toString()] : []}
                                                value={selectedAccount
                                                    ? `${selectedAccount.connectedAs}${selectedAccount.isPrimary ? ' (Primary)' : ''}`
                                                    : ''}
                                                onOptionSelect={(_event, data) => {
                                                    if (!data.optionValue) {
                                                        return
                                                    }

                                                    const parsed = Number(data.optionValue)
                                                    if (!Number.isNaN(parsed)) {
                                                        setSelectedAccountId(parsed)
                                                    }
                                                }}
                                            >
                                                {gitHubConnections.map((account) => {
                                                    const accountLabel = `${account.connectedAs}${account.isPrimary ? ' (Primary)' : ''}`
                                                    return (
                                                        <Option
                                                            key={account.id}
                                                            value={account.id.toString()}
                                                            text={accountLabel}
                                                        >
                                                            {accountLabel}
                                                        </Option>
                                                    )
                                                })}
                                            </Dropdown>
                                        ) : null}
                                    </Field>

                                    <Field
                                        label="New Repository Name"
                                        required
                                        validationState={normalizedNewRepoName.length > 0 && (!newRepoNameValid || isNewRepoNameTaken) ? 'error' : undefined}
                                        validationMessage={normalizedNewRepoName.length > 0
                                            ? (!newRepoNameValid
                                                ? 'Use letters, numbers, dots, underscores, or dashes.'
                                                : (isNewRepoNameTaken ? 'This repository name is already in use for the selected account.' : undefined))
                                            : undefined}
                                    >
                                        <Input
                                            placeholder="my-new-repo"
                                            value={newRepoName}
                                            onChange={(_event, data) => setNewRepoName(data.value)}
                                        />
                                        {canCheckNewRepoAvailability && !selectedAccountRepoErrorMessage && (
                                            <div className={styles.repoAvailabilityRow}>
                                                {isCheckingNewRepoAvailability ? (
                                                    <Spinner size="extra-tiny" />
                                                ) : isNewRepoNameTaken ? (
                                                    <DismissCircle16Filled className={styles.repoAvailabilityUnavailable} />
                                                ) : (
                                                    <CheckmarkCircle16Filled className={styles.repoAvailabilityAvailable} />
                                                )}
                                                <Text className={styles.repoAvailabilityText}>
                                                    {selectedAccountLogin}/{normalizedNewRepoName}
                                                </Text>
                                                <Caption1 className={isNewRepoNameTaken ? styles.repoAvailabilityUnavailable : styles.repoAvailabilityAvailable}>
                                                    {isCheckingNewRepoAvailability
                                                        ? 'Checking...'
                                                        : (isNewRepoNameTaken ? 'Unavailable' : 'Available')}
                                                </Caption1>
                                            </div>
                                        )}
                                    </Field>

                                    <Field label="Repository Description">
                                        <Textarea
                                            placeholder="Optional description for the new repository"
                                            resize="vertical"
                                            rows={2}
                                            value={newRepoDescription}
                                            onChange={(_event, data) => setNewRepoDescription(data.value)}
                                        />
                                    </Field>

                                    <Checkbox
                                        className={styles.privacyToggle}
                                        label="Make repository private"
                                        checked={newRepoPrivate}
                                        onChange={(_event, data) => setNewRepoPrivate(!!data.checked)}
                                    />
                                </div>
                            )}
                            {submissionError && (
                                <Text className={styles.formError}>{submissionError}</Text>
                            )}
                        </div>
                    </DialogContent>
                    <DialogActions className={mergeClasses(isMobile && styles.dialogActionsMobile)}>
                        <Button
                            appearance="secondary"
                            onClick={() => { onOpenChange(false); resetForm() }}
                            className={mergeClasses(isMobile && styles.dialogActionButtonMobile)}
                        >
                            Cancel
                        </Button>
                        <Button
                            appearance="primary"
                            onClick={() => void handleCreate()}
                            disabled={!canCreate}
                            className={mergeClasses(isMobile && styles.dialogActionButtonMobile)}
                        >
                            {createMutation.isPending
                                ? 'Creating Project...'
                                : createRepoMutation.isPending
                                    ? 'Creating Repository...'
                                    : 'Create Project'}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    )
}
