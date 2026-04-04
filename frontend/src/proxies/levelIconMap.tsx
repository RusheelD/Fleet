import type { ReactNode } from 'react'
import {
    TargetRegular,
    PuzzlePieceRegular,
    BookRegular,
    BugRegular,
    TaskListLtrRegular,
    LightbulbRegular,
    FlashRegular,
    StarRegular,
    HeartRegular,
    FlagRegular,
    ShieldRegular,
    WrenchRegular,
    ChatRegular,
    CodeRegular,
    BeakerRegular,
    CheckmarkCircleRegular,
    CircleRegular,
    DiamondRegular,
    SearchRegular,
    PersonRegular,
    LockClosedRegular,
    GlobeRegular,
    ClipboardRegular,
    DocumentRegular,
    CalendarRegular,
    TagRegular,
} from '@fluentui/react-icons'
import { FleetRocketLogo } from '../components/shared'

/**
 * Maps level icon name strings from the API to Fluent UI React icon components.
 * Used by <LevelBadge> and the level management UI.
 */
const levelIconMap: Record<string, ReactNode> = {
    'bullseye': <TargetRegular />,
    'rocket': <FleetRocketLogo size={16} title="Rocket" variant="outline" />,
    'puzzle-piece': <PuzzlePieceRegular />,
    'book': <BookRegular />,
    'bug': <BugRegular />,
    'task-list': <TaskListLtrRegular />,
    'lightbulb': <LightbulbRegular />,
    'lightning': <FlashRegular />,
    'star': <StarRegular />,
    'heart': <HeartRegular />,
    'flag': <FlagRegular />,
    'shield': <ShieldRegular />,
    'wrench': <WrenchRegular />,
    'chat': <ChatRegular />,
    'code': <CodeRegular />,
    'beaker': <BeakerRegular />,
    'checkmark': <CheckmarkCircleRegular />,
    'circle': <CircleRegular />,
    'diamond': <DiamondRegular />,
    'search': <SearchRegular />,
    'person': <PersonRegular />,
    'lock': <LockClosedRegular />,
    'globe': <GlobeRegular />,
    'clipboard': <ClipboardRegular />,
    'document': <DocumentRegular />,
    'calendar': <CalendarRegular />,
    'tag': <TagRegular />,
}

/** All available icon names for the level icon picker */
export const LEVEL_ICON_NAMES = Object.keys(levelIconMap)

/** Resolve a level icon name to a Fluent UI icon component */
export function resolveLevelIcon(name: string): ReactNode {
    return levelIconMap[name] ?? <CircleRegular />
}
