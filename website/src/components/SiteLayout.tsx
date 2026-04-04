import { Outlet } from 'react-router-dom'
import { makeStyles } from '@fluentui/react-components'
import { Navbar } from './'
import { Footer } from './'
import { appTokens } from '../styles/appTokens'

const useStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        minHeight: '100vh',
        backgroundColor: appTokens.color.pageBackground,
        color: appTokens.color.textPrimary,
        width: '100%',
    },
    content: {
        flexGrow: 1,
        minWidth: 0,
        width: '100%',
        overflowX: 'hidden',
    },
})

export function SiteLayout() {
    const styles = useStyles()

    return (
        <div className={styles.root}>
            <Navbar />
            <main className={styles.content}>
                <Outlet />
            </main>
            <Footer />
        </div>
    )
}
