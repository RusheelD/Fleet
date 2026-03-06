export function formatWorkItemState(state: string): string {
  switch (state) {
    case 'In-PR':
      return 'In PR'
    case 'In-PR (AI)':
      return 'In PR (AI)'
    default:
      return state
  }
}
