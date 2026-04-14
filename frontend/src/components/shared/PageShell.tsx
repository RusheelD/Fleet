import type { ReactNode } from 'react'
import { makeStyles, mergeClasses } from '@fluentui/react-components'
import { useIsMobile } from '../../hooks/useIsMobile'
import { appTokens } from '../../styles/appTokens'
import { PageHeader } from './PageHeader'

type PageShellWidth = 'narrow' | 'medium' | 'large'

interface PageShellProps {
  title: string
  subtitle?: string
  actions?: ReactNode
  maxWidth?: PageShellWidth
  children: ReactNode
}

const useStyles = makeStyles({
  page: {
    paddingTop: appTokens.space.xl,
    paddingRight: appTokens.space.pageX,
    paddingBottom: appTokens.space.xl,
    paddingLeft: appTokens.space.pageX,
    margin: '0 auto',
    width: '100%',
    minWidth: 0,
  },
  pageMobile: {
    paddingTop: appTokens.space.pageYMobile,
    paddingBottom: appTokens.space.pageYMobile,
    paddingLeft: appTokens.space.pageXMobile,
    paddingRight: appTokens.space.pageXMobile,
  },
  widthNarrow: {
    maxWidth: appTokens.width.pageNarrow,
  },
  widthMedium: {
    maxWidth: appTokens.width.pageMedium,
  },
  widthLarge: {
    maxWidth: appTokens.width.pageLarge,
  },
  content: {
    display: 'flex',
    flexDirection: 'column',
    gap: appTokens.space.lg,
  },
})

export function PageShell({
  title,
  subtitle,
  actions,
  maxWidth = 'medium',
  children,
}: PageShellProps) {
  const styles = useStyles()
  const isMobile = useIsMobile()

  const widthClassName = maxWidth === 'narrow'
    ? styles.widthNarrow
    : maxWidth === 'large'
      ? styles.widthLarge
      : styles.widthMedium

  return (
    <div className={mergeClasses(styles.page, widthClassName, isMobile && styles.pageMobile)}>
      <PageHeader title={title} subtitle={subtitle} actions={actions} />
      <div className={styles.content}>
        {children}
      </div>
    </div>
  )
}
