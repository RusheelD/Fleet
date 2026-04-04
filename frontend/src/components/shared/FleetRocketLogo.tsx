import type { CSSProperties } from 'react'
import { makeStyles, mergeClasses } from '@fluentui/react-components'

const useStyles = makeStyles({
    logo: {
        display: 'block',
        flexShrink: 0,
    },
})

type FleetRocketLogoCssVars = CSSProperties & {
    '--rocket-primary'?: string
    '--rocket-stroke'?: string
    '--rocket-window'?: string
    '--rocket-flame'?: string
}

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
    const colorStyle: FleetRocketLogoCssVars = {
        width: resolvedSize,
        height: resolvedSize,
        '--rocket-primary': isOutline ? 'transparent' : 'var(--fleet-rocket-primary)',
        '--rocket-stroke': isOutline ? 'currentColor' : 'var(--fleet-rocket-stroke)',
        '--rocket-window': isOutline ? 'transparent' : 'var(--fleet-rocket-window)',
        '--rocket-flame': isOutline ? 'currentColor' : 'var(--fleet-rocket-flame)',
    }

    return (
        <svg
            xmlns="http://www.w3.org/2000/svg"
            width="1024"
            height="1024"
            viewBox="0 0 1024 1024"
            fill="none"
            className={mergeClasses(styles.logo, 'fleet-rocket-logo', className)}
            style={colorStyle}
            role={title ? 'img' : undefined}
            aria-hidden={title ? undefined : true}
            aria-label={title}
        >
            {title ? <title>{title}</title> : null}
            <style>
                {`
                    .rocket-fill { fill: var(--rocket-primary, currentColor); }
                    .rocket-stroke { stroke: var(--rocket-stroke, currentColor); }
                    .rocket-window { fill: var(--rocket-window, currentColor); }
                    .rocket-flame { fill: var(--rocket-flame, currentColor); }
                `}
            </style>

            <g transform="translate(512 512) scale(1.5) translate(-512 -512)">
                <g transform="translate(512 512) scale(-1,1) translate(-512 -512)">
                    <g transform="translate(512 512) rotate(-35) translate(-512 -512)">
                        <g
                            className="rocket-stroke"
                            strokeWidth="28"
                            strokeLinecap="round"
                            strokeLinejoin="round"
                        >
                            <path
                                className="rocket-fill"
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
                                className="rocket-fill"
                                d="M420 560
                                   C370 585 330 635 312 708
                                   C366 686 414 650 450 602 Z"
                            />

                            <path
                                className="rocket-fill"
                                d="M604 560
                                   C654 585 694 635 712 708
                                   C658 686 610 650 574 602 Z"
                            />

                            <path
                                className="rocket-window rocket-stroke"
                                d="M512 320 L552 388 L472 388 Z"
                                strokeWidth="22"
                            />
                        </g>

                        <path
                            className="rocket-flame"
                            d="M512 706
                               C546 758 540 830 512 884
                               C484 830 478 758 512 706 Z"
                        />
                    </g>
                </g>
            </g>
        </svg>
    )
}
