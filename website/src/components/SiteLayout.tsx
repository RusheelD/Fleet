import { Outlet } from 'react-router-dom'
import { makeStyles, tokens } from '@fluentui/react-components'
import { Navbar } from './'
import { Footer } from './'

const useStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        minHeight: '100vh',
        backgroundColor: tokens.colorNeutralBackground1,
        width: '100%',
    },
    content: {
        flexGrow: 1,
        minWidth: 0,
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
