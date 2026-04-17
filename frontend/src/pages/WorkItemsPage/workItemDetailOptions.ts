export const NONE_PARENT = '(None)'
export const NONE_LEVEL = '(None)'

export const PRIORITY_LABELS: Record<number, string> = {
    1: 'P1 - Critical',
    2: 'P2 - High',
    3: 'P3 - Medium',
    4: 'P4 - Low',
}

export const PRIORITY_MAP: Record<string, number> = {
    'P1 - Critical': 1,
    'P2 - High': 2,
    'P3 - Medium': 3,
    'P4 - Low': 4,
}

export const DIFFICULTY_LABELS: Record<number, string> = {
    1: 'D1 - Very Easy',
    2: 'D2 - Easy',
    3: 'D3 - Medium',
    4: 'D4 - Hard',
    5: 'D5 - Very Hard',
}

export const DIFFICULTY_MAP: Record<string, number> = {
    'D1 - Very Easy': 1,
    'D2 - Easy': 2,
    'D3 - Medium': 3,
    'D4 - Hard': 4,
    'D5 - Very Hard': 5,
}
