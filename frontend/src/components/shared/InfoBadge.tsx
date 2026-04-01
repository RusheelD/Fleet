import { Badge, makeStyles, mergeClasses, type BadgeProps } from '@fluentui/react-components'
import { appTokens } from '../../styles/appTokens'

type InfoBadgeAppearance = 'filled' | 'ghost' | 'outline' | 'tint'

interface InfoBadgeProps extends Omit<BadgeProps, 'appearance' | 'color'> {
    appearance?: InfoBadgeAppearance
}

const useStyles = makeStyles({
    filled: {
        backgroundColor: appTokens.color.info,
        color: appTokens.color.textOnBrand,
        '::after': {
            borderTopColor: appTokens.color.info,
            borderRightColor: appTokens.color.info,
            borderBottomColor: appTokens.color.info,
            borderLeftColor: appTokens.color.info,
        },
    },
    ghost: {
        color: appTokens.color.info,
    },
    outline: {
        color: appTokens.color.info,
        '::after': {
            borderTopColor: appTokens.color.info,
            borderRightColor: appTokens.color.info,
            borderBottomColor: appTokens.color.info,
            borderLeftColor: appTokens.color.info,
        },
    },
    tint: {
        backgroundColor: appTokens.color.infoSurface,
        color: appTokens.color.info,
        '::after': {
            borderTopColor: appTokens.color.infoBorder,
            borderRightColor: appTokens.color.infoBorder,
            borderBottomColor: appTokens.color.infoBorder,
            borderLeftColor: appTokens.color.infoBorder,
        },
    },
})

export function InfoBadge({
    appearance = 'filled',
    className,
    ...props
}: InfoBadgeProps) {
    const styles = useStyles()

    return (
        <Badge
            {...props}
            appearance={appearance}
            color="subtle"
            className={mergeClasses(styles[appearance], className)}
        />
    )
}
