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
}

export function FleetRocketLogo({
    className,
    size = 24,
    title,
}: FleetRocketLogoProps) {
    const styles = useStyles()
    const resolvedSize = typeof size === 'number' ? `${size}px` : size
    const colorStyle: FleetRocketLogoCssVars = {
        width: resolvedSize,
        height: resolvedSize,
        '--rocket-primary': 'var(--fleet-rocket-primary)',
        '--rocket-stroke': 'var(--fleet-rocket-stroke)',
        '--rocket-window': 'var(--fleet-rocket-window)',
        '--rocket-flame': 'var(--fleet-rocket-flame)',
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
                    .rocket-body { fill: var(--rocket-primary, currentColor); }
                    .rocket-stroke { stroke: var(--rocket-stroke, currentColor); }
                    .rocket-window { fill: var(--rocket-window, currentColor); }
                    .rocket-flame { fill: var(--rocket-flame, currentColor); }
                `}
            </style>

            <g transform="translate(512 512) scale(-1,1) translate(-512 -512)">
                <g
                    transform="translate(512 512) rotate(-35) translate(-512 -512)"
                    className="rocket-stroke"
                    strokeWidth="18"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                >
                    <path
                        className="rocket-body"
                        d="M512 180
                           C590 220 632 320 620 470
                           L606 640
                           C603 676 578 700 542 700
                           H482
                           C446 700 421 676 418 640
                           L404 470
                           C392 320 434 220 512 180 Z"
                    />

                    <path
                        className="rocket-body"
                        d="M512 112
                           C550 136 573 170 582 214
                           C537 198 487 198 442 214
                           C451 170 474 136 512 112 Z"
                    />

                    <path
                        className="rocket-window"
                        d="M512 300
                           L560 380
                           L464 380
                           Z"
                        strokeWidth="18"
                        strokeLinejoin="round"
                    />

                    <path
                        className="rocket-body"
                        d="M418 570
                           C360 590 320 640 304 714
                           C360 694 406 662 438 616 Z"
                    />

                    <path
                        className="rocket-body"
                        d="M606 570
                           C664 590 704 640 720 714
                           C664 694 618 662 586 616 Z"
                    />

                    <path className="rocket-body" d="M350 420 H412 V700 H350 Z" />
                    <path className="rocket-body" d="M381 380 L405 420 H357 Z" />

                    <path className="rocket-body" d="M612 420 H674 V700 H612 Z" />
                    <path className="rocket-body" d="M643 380 L667 420 H619 Z" />

                    <path className="rocket-body" d="M474 700 H550 L538 770 H486 Z" />
                    <path className="rocket-body" d="M362 700 H400 L394 754 H368 Z" />
                    <path className="rocket-body" d="M624 700 H662 L656 754 H630 Z" />

                    <g className="rocket-flame" stroke="none">
                        <path
                            d="M381 754
                               C398 784 396 820 381 848
                               C366 820 364 784 381 754 Z"
                        />

                        <path
                            d="M643 754
                               C660 784 658 820 643 848
                               C628 820 626 784 643 754 Z"
                        />

                        <path
                            d="M512 770
                               C540 818 536 880 512 924
                               C488 880 484 818 512 770 Z"
                        />
                    </g>
                </g>
            </g>
        </svg>
    )
}
