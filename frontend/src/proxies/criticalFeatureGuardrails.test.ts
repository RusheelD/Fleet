import { afterEach, describe, expect, it, vi } from 'vitest'
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative, sep } from 'node:path'
import { startExecution, steerExecution } from './agentsProxy'
import { createLoginProviderLinkState, deleteLoginIdentity } from './authProxy'
import { createChatSession, sendChatMessage } from './chatProxy'
import { createWorkItemLevel } from './levelsProxy'
import { createProject, updateProject } from './projectsProxy'
import { setTokenGetter } from './proxy'
import {
  createGitHubRepo,
  createMcpServer,
  createProjectMemory,
  createProjectSkill,
  linkGitHub,
  setPrimaryGitHubAccount,
  validateMcpServer,
} from './userProxy'
import { createWorkItem, updateWorkItem } from './workItemsProxy'

describe('critical feature API guardrails', () => {
  afterEach(() => {
    setTokenGetter(() => Promise.resolve(undefined))
    vi.unstubAllGlobals()
  })

  it('keeps login provider linking on the auth API contract', async () => {
    const fetchMock = installFetchMock({ state: 'protected-state' })

    await createLoginProviderLinkState('microsoft')
    await deleteLoginIdentity(12)

    expectFetchCall(fetchMock, 1, '/api/auth/login-identities/link-state', 'POST', {
      provider: 'microsoft',
    })
    expectFetchCall(fetchMock, 2, '/api/auth/login-identities/12', 'DELETE')
  })

  it('keeps project, work item, and level mutations project scoped', async () => {
    const fetchMock = installFetchMock({})

    await createProject({ title: 'Project', description: 'Desc', repo: 'owner/repo' })
    await updateProject('proj-1', { title: 'Renamed' })
    await createWorkItem('proj-1', {
      title: 'Task',
      description: 'Desc',
      priority: 1,
      difficulty: 2,
      state: 'New',
      assignedTo: 'Fleet',
      tags: [],
      isAI: false,
      parentWorkItemNumber: null,
      levelId: null,
    })
    await updateWorkItem('proj-1', 7, { state: 'Active' })
    await createWorkItemLevel('proj-1', {
      name: 'Feature',
      iconName: 'TaskList',
      color: '#0078d4',
      ordinal: 1,
    })

    expectFetchCall(fetchMock, 1, '/api/projects', 'POST')
    expectFetchCall(fetchMock, 2, '/api/projects/proj-1', 'PUT')
    expectFetchCall(fetchMock, 3, '/api/projects/proj-1/work-items', 'POST')
    expectFetchCall(fetchMock, 4, '/api/projects/proj-1/work-items/7', 'PUT', { state: 'Active' })
    expectFetchCall(fetchMock, 5, '/api/projects/proj-1/levels', 'POST')
  })

  it('keeps chat normal sends and dynamic iteration sends on explicit contracts', async () => {
    const fetchMock = installFetchMock({ id: 'sess-1' })

    await createChatSession('proj-1', 'New Chat')
    await sendChatMessage('proj-1', 'sess-1', { content: 'hello', generateWorkItems: false })
    await sendChatMessage('proj-1', 'sess-1', {
      content: 'Implement retry handling',
      generateWorkItems: true,
      dynamicOptions: {
        enabled: true,
        branchName: 'feature/retry',
        strategy: 'parallel',
      },
    })

    expectFetchCall(fetchMock, 1, '/api/projects/proj-1/chat/sessions', 'POST', { title: 'New Chat' })
    expectFetchCall(fetchMock, 2, '/api/projects/proj-1/chat/sessions/sess-1/messages', 'POST', {
      content: 'hello',
      generateWorkItems: false,
    })
    expectFetchCall(fetchMock, 3, '/api/projects/proj-1/chat/sessions/sess-1/messages', 'POST', {
      content: 'Implement retry handling',
      generateWorkItems: true,
      dynamicIteration: {
        enabled: true,
        executionPolicy: 'parallel',
        targetBranch: 'feature/retry',
      },
    })
  })

  it('keeps agent execution controls project scoped', async () => {
    const fetchMock = installFetchMock({ executionId: 'exec-1', status: 'running' })

    await startExecution('proj-1', { workItemNumber: 9, targetBranch: 'feature/agent' })
    await steerExecution('proj-1', 'exec-1', 'Use the existing retry helper.')

    expectFetchCall(fetchMock, 1, '/api/projects/proj-1/agents/execute', 'POST', {
      workItemNumber: 9,
      targetBranch: 'feature/agent',
    })
    expectFetchCall(fetchMock, 2, '/api/projects/proj-1/agents/executions/exec-1/steer', 'POST', {
      note: 'Use the existing retry helper.',
    })
  })

  it('keeps integration, memory, skill, and MCP mutations on their scoped APIs', async () => {
    const fetchMock = installFetchMock({})

    await linkGitHub('code', 'https://app.fleet-ai.dev', 'state')
    await setPrimaryGitHubAccount(4)
    await createGitHubRepo({ name: 'fleet-demo', private: true, accountId: 4 })
    await createProjectMemory('proj-1', {
      name: 'API style',
      description: 'Use controller-service-repository.',
      type: 'project',
      content: 'Keep controllers thin.',
      alwaysInclude: true,
    })
    await createProjectSkill('proj-1', {
      name: 'Reviewer',
      description: 'Review code',
      whenToUse: 'Before merge',
      content: 'Check regressions.',
      enabled: true,
    })
    await createMcpServer({
      name: 'docs',
      transportType: 'http',
      endpoint: 'https://example.com/mcp',
      enabled: true,
    })
    await validateMcpServer(8)

    expectFetchCall(fetchMock, 1, '/api/connections/github', 'POST')
    expectFetchCall(fetchMock, 2, '/api/connections/github/4/primary', 'PUT')
    expectFetchCall(fetchMock, 3, '/api/connections/github/repos', 'POST')
    expectFetchCall(fetchMock, 4, '/api/projects/proj-1/memories', 'POST')
    expectFetchCall(fetchMock, 5, '/api/projects/proj-1/skills', 'POST')
    expectFetchCall(fetchMock, 6, '/api/mcp-servers', 'POST')
    expectFetchCall(fetchMock, 7, '/api/mcp-servers/8/validate', 'POST', {})
  })

  it('keeps direct /api calls centralized in proxies and approved infrastructure hooks', () => {
    const allowedApiCallFiles = new Set([
      normalizePath('src/hooks/PreferencesProvider.tsx'),
      normalizePath('src/hooks/useAuth.tsx'),
      normalizePath('src/hooks/useServerEvents.ts'),
    ])
    const violations = listSourceFiles(join(process.cwd(), 'src'))
      .filter((filePath) => !normalizePath(relative(process.cwd(), filePath)).startsWith('src/proxies/'))
      .filter((filePath) => !normalizePath(relative(process.cwd(), filePath)).endsWith('.test.ts'))
      .filter((filePath) => !normalizePath(relative(process.cwd(), filePath)).endsWith('.test.tsx'))
      .filter((filePath) => readFileSync(filePath, 'utf8').includes('/api/'))
      .map((filePath) => normalizePath(relative(process.cwd(), filePath)))
      .filter((relativePath) => !allowedApiCallFiles.has(relativePath))

    expect(violations).toEqual([])
  })
})

function installFetchMock(body: unknown) {
  const responseText = JSON.stringify(body)
  const fetchMock = vi.fn().mockImplementation(() => Promise.resolve(new Response(responseText, { status: 200 })))
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function expectFetchCall(
  fetchMock: ReturnType<typeof vi.fn>,
  callNumber: number,
  path: string,
  method: string,
  body?: unknown,
) {
  const [actualPath, init] = fetchMock.mock.calls[callNumber - 1] as [string, RequestInit]
  expect(actualPath).toBe(path)
  expect(init.method).toBe(method)

  if (body !== undefined) {
    expect(JSON.parse(String(init.body))).toEqual(body)
  }
}

function listSourceFiles(root: string): string[] {
  return readdirSync(root).flatMap((entry) => {
    const filePath = join(root, entry)
    const stat = statSync(filePath)
    if (stat.isDirectory()) {
      return listSourceFiles(filePath)
    }

    return filePath.endsWith('.ts') || filePath.endsWith('.tsx') ? [filePath] : []
  })
}

function normalizePath(path: string): string {
  return path.split(sep).join('/')
}
