import { makeStyles, mergeClasses } from '@fluentui/react-components'

const useStyles = makeStyles({
    logo: {
        display: 'block',
        flexShrink: 0,
    },
})

interface FleetRocketLogoProps {
    className?: string
    size?: number | string
    title?: string
    variant?: 'brand' | 'outline'
}

export function FleetRocketLogo({
    className,
    size = 24,
    title,
    variant = 'brand',
}: FleetRocketLogoProps) {
    const styles = useStyles()
    const resolvedSize = typeof size === 'number' ? `${size}px` : size
    const isOutline = variant === 'outline'

    return (
        <svg
            xmlns="http://www.w3.org/2000/svg"
            width="1024"
            height="1024"
            viewBox="0 0 1024 1024"
            fill="none"
            className={mergeClasses(styles.logo, className)}
            style={{
                width: resolvedSize,
                height: resolvedSize,
                color: isOutline ? 'currentColor' : undefined,
            }}
            role={title ? 'img' : undefined}
            aria-hidden={title ? undefined : true}
            aria-label={title}
        >
            {title ? <title>{title}</title> : null}
            <g transform="translate(512 512) scale(-1,1) translate(-512 -512)">
                <g transform="translate(512 512) rotate(-35) translate(-512 -512)">
                    <g
                        stroke={isOutline ? 'currentColor' : '#261617'}
                        strokeWidth="28"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                    >
                        <path
                            fill={isOutline ? 'transparent' : '#EC4B5F'}
                            d="M512 180
                               C590 220 632 330 620 500
                               L610 620
                               C606 670 572 704 522 704
                               H502
                               C452 704 418 670 414 620
                               L404 500
                               C392 330 434 220 512 180 Z"
                        />
                        <path
                            fill={isOutline ? 'transparent' : '#EC4B5F'}
                            d="M420 560
                               C370 585 330 635 312 708
                               C366 686 414 650 450 602 Z"
                        />
                        <path
                            fill={isOutline ? 'transparent' : '#EC4B5F'}
                            d="M604 560
                               C654 585 694 635 712 708
                               C658 686 610 650 574 602 Z"
                        />
                        <path
                            fill={isOutline ? 'transparent' : '#261617'}
                            d="M512 320 L552 388 L472 388 Z"
                            strokeWidth="22"
                        />
                    </g>
                    <path
                        fill={isOutline ? 'currentColor' : '#F5A04C'}
                        d="M512 706
                           C546 758 540 830 512 884
                           C484 830 478 758 512 706 Z"
                    />
                </g>
            </g>
        </svg>
    )
}
