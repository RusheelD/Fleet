import type { ReactNode } from 'react'
import {
    BranchRegular,
    CheckmarkCircleRegular,
    BotRegular,
    CodeRegular,
    PersonRegular,
    BoardRegular,
    ArrowTrendingRegular,
    RocketRegular,
    ChatRegular,
    DiamondRegular,
    SparkleRegular,
    QuestionCircleRegular,
    HistoryRegular,
} from '@fluentui/react-icons'

const iconMap: Record<string, ReactNode> = {
    branch: <BranchRegular />,
    checkmark: <CheckmarkCircleRegular />,
    bot: <BotRegular />,
    code: <CodeRegular />,
    person: <PersonRegular />,
    board: <BoardRegular />,
    trending: <ArrowTrendingRegular />,
    rocket: <RocketRegular />,
    chat: <ChatRegular />,
    diamond: <DiamondRegular />,
    sparkle: <SparkleRegular />,
    commit: <HistoryRegular />,
}

/** Map an icon name string from the API to a Fluent UI React icon component */
export function resolveIcon(name: string): ReactNode {
    return iconMap[name] ?? <QuestionCircleRegular />
}
