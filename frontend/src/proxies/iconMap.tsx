import type { ReactNode } from 'react'
import {
    BranchRegular,
    CheckmarkCircleRegular,
    BotRegular,
    CodeRegular,
    PersonRegular,
    BoardRegular,
    ArrowTrendingRegular,
    ChatRegular,
    DiamondRegular,
    SparkleRegular,
    QuestionCircleRegular,
    HistoryRegular,
} from '@fluentui/react-icons'
import { FleetRocketLogo } from '../components/shared/FleetRocketLogo'

const iconMap: Record<string, ReactNode> = {
    branch: <BranchRegular />,
    checkmark: <CheckmarkCircleRegular />,
    bot: <BotRegular />,
    code: <CodeRegular />,
    person: <PersonRegular />,
    board: <BoardRegular />,
    trending: <ArrowTrendingRegular />,
    rocket: <FleetRocketLogo size={16} title="Rocket" variant="outline" />,
    chat: <ChatRegular />,
    diamond: <DiamondRegular />,
    sparkle: <SparkleRegular />,
    commit: <HistoryRegular />,
}

/** Map an icon name string from the API to a Fluent UI React icon component */
export function resolveIcon(name: string): ReactNode {
    return iconMap[name] ?? <QuestionCircleRegular />
}
