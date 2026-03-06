import { useState, useMemo, type SyntheticEvent } from 'react'
import {
    makeStyles,
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
    Option,
    Caption1,
} from '@fluentui/react-components'
import { CheckmarkCircle16Filled, DismissCircle16Filled, LockClosedRegular } from '@fluentui/react-icons'
import { useCreateProject, useCheckSlug, useGitHubRepos, useUserSettings } from '../../proxies'
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

const useStyles = makeStyles({
    dialogSurface: {
        width: 'min(680px, calc(100vw - 2rem))',
    },
    dialogForm: {
        display: 'flex',
        flexDirection: 'column',
        gap: '1.125rem',
    },
    repoHint: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
    },
    slugRow: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        marginTop: tokens.spacingVerticalXS,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
        paddingTop: '2px',
        paddingBottom: '2px',
        paddingLeft: '6px',
        paddingRight: '6px',
        width: 'fit-content',
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
    privateIcon: {
        color: tokens.colorNeutralForeground3,
        fontSize: '12px',
        marginLeft: '4px',
    },
    repoCombobox: {
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
    const createMutation = useCreateProject()

    const [title, setTitle] = useState('')
    const [description, setDescription] = useState('')
    const [repo, setRepo] = useState('')
    const [repoSearch, setRepoSearch] = useState('')

    // Check if GitHub is linked
    const { data: settings } = useUserSettings()
    const hasGitHub = settings?.connections.some(c => c.provider === 'GitHub' && c.connectedAs) ?? false

    // Fetch repos only when GitHub is linked and dialog is open
    const { data: repos, isLoading: isLoadingRepos } = useGitHubRepos(hasGitHub && open)

    const filteredRepos = useMemo(() => {
        if (!repos) return []
        if (!repoSearch) return repos
        const q = repoSearch.toLowerCase()
        return repos
            .map(r => {
                const name = r.fullName.toLowerCase()
                const desc = r.description?.toLowerCase() ?? ''
                const nameIdx = name.indexOf(q)
                const descIdx = desc.indexOf(q)
                if (nameIdx === -1 && descIdx === -1) return null
                // Lower score = higher priority.
                // Name matches score 0-999, desc matches score 1000+.
                // Earlier position in the string = lower score within each tier.
                const score = nameIdx !== -1 ? nameIdx : 1000 + descIdx
                return { repo: r, score }
            })
            .filter((x): x is { repo: typeof repos[number]; score: number } => x !== null)
            .sort((a, b) => a.score - b.score)
            .map(x => x.repo)
    }, [repos, repoSearch])

    const slugPreview = useMemo(() => generateSlugPreview(title), [title])
    const slugCheck = useCheckSlug(slugPreview)

    const slugAvailable = slugCheck.data?.available === true
    const slugUnavailable = slugCheck.data?.available === false && slugPreview.length > 0
    const isCheckingSlug = slugCheck.isLoading && slugPreview.length > 0
    const hasSelectedRepo = repos?.some(r => r.fullName === repo.trim()) ?? false

    const canCreate = canCreateProject({
        title,
        slugPreview,
        slugAvailable,
        hasGitHub,
        hasSelectedRepo,
        isPending: createMutation.isPending,
    })

    const resetForm = () => {
        setTitle('')
        setDescription('')
        setRepo('')
        setRepoSearch('')
    }

    const handleCreate = () => {
        if (!canCreate) return
        createMutation.mutate(
            {
                title: title.trim(),
                description: description.trim(),
                repo: repo.trim(),
            },
            {
                onSuccess: (project) => {
                    resetForm()
                    onOpenChange(false)
                    onCreated?.(project.slug)
                },
            },
        )
    }

    return (
        <Dialog open={open} onOpenChange={(_e, data) => { onOpenChange(data.open); if (!data.open) resetForm() }}>
            <DialogSurface className={styles.dialogSurface}>
                <DialogBody>
                    <DialogTitle>Create New Project</DialogTitle>
                    <DialogContent>
                        <div className={styles.dialogForm}>
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
                            <Field
                                label="GitHub Repository"
                                required
                                validationState={repo.trim().length > 0 && !hasSelectedRepo ? 'error' : undefined}
                                validationMessage={repo.trim().length > 0 && !hasSelectedRepo ? 'Select a repository from your linked GitHub list.' : undefined}
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
                                        placeholder={isLoadingRepos ? 'Loading repositories...' : 'Search your repositories...'}
                                        value={repo || repoSearch}
                                        freeform
                                        onInput={(e: SyntheticEvent<HTMLInputElement>) => {
                                            const newValue = e.currentTarget.value
                                            setRepoSearch(newValue)
                                            // If the input doesn't match any repo, clear the selection
                                            if (!repos?.some(r => r.fullName === newValue)) {
                                                setRepo('')
                                            }
                                        }}
                                        onOptionSelect={(_e: SyntheticEvent<HTMLElement>, data: { optionValue?: string }) => {
                                            if (data.optionValue) {
                                                // User selected an option from the dropdown
                                                setRepo(data.optionValue)
                                                setRepoSearch('')
                                            }
                                        }}
                                    >
                                        {isLoadingRepos ? (
                                            <Option disabled key="__loading" text="">
                                                <Spinner size="tiny" label="Loading repos..." />
                                            </Option>
                                        ) : filteredRepos.length > 0 ? (
                                            filteredRepos.map(r => (
                                                <Option key={r.fullName} value={r.fullName} text={r.fullName}>
                                                    <div className={styles.repoOption}>
                                                        <Text className={styles.repoName}>
                                                            {r.fullName}
                                                            {r.private && <LockClosedRegular className={styles.privateIcon} />}
                                                        </Text>
                                                        {r.description && (
                                                            <Caption1 className={styles.repoDescription}>
                                                                {r.description}
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
                        </div>
                    </DialogContent>
                    <DialogActions>
                        <Button appearance="secondary" onClick={() => { onOpenChange(false); resetForm() }}>
                            Cancel
                        </Button>
                        <Button
                            appearance="primary"
                            onClick={handleCreate}
                            disabled={!canCreate}
                        >
                            {createMutation.isPending ? 'Creating...' : 'Create Project'}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    )
}

