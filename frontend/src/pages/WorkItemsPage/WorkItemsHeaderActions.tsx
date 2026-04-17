import type { ChangeEvent, RefObject } from 'react'
import { makeStyles, mergeClasses, Button } from '@fluentui/react-components'
import { ArrowUploadRegular, ArrowDownloadRegular, AddRegular } from '@fluentui/react-icons'

const useStyles = makeStyles({
    actions: {
        display: 'flex',
        gap: '0.75rem',
        alignItems: 'center',
        flexWrap: 'wrap',
    },
    actionsMobile: {
        width: '100%',
    },
    mobileButton: {
        flex: '1 1 140px',
    },
})

interface WorkItemsHeaderActionsProps {
    isMobile: boolean
    importFileInputRef: RefObject<HTMLInputElement | null>
    onImportFileChange: (event: ChangeEvent<HTMLInputElement>) => void
    onImportClick: () => void
    onExport: () => void
    onCreate: () => void
    importPending: boolean
    exportPending: boolean
    canExport: boolean
}

export function WorkItemsHeaderActions({
    isMobile,
    importFileInputRef,
    onImportFileChange,
    onImportClick,
    onExport,
    onCreate,
    importPending,
    exportPending,
    canExport,
}: WorkItemsHeaderActionsProps) {
    const styles = useStyles()

    return (
        <div className={mergeClasses(styles.actions, isMobile && styles.actionsMobile)}>
            <input
                ref={importFileInputRef}
                type="file"
                accept=".json,application/json"
                style={{ display: 'none' }}
                onChange={onImportFileChange}
            />
            <Button
                appearance="secondary"
                icon={<ArrowUploadRegular />}
                onClick={onImportClick}
                disabled={importPending}
                className={mergeClasses(isMobile && styles.mobileButton)}
            >
                Import
            </Button>
            <Button
                appearance="secondary"
                icon={<ArrowDownloadRegular />}
                onClick={onExport}
                disabled={exportPending || !canExport}
                className={mergeClasses(isMobile && styles.mobileButton)}
            >
                Export
            </Button>
            <Button
                appearance="primary"
                icon={<AddRegular />}
                onClick={onCreate}
                className={mergeClasses(isMobile && styles.mobileButton)}
            >
                New Work Item
            </Button>
        </div>
    )
}
