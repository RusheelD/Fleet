import { fileURLToPath, URL } from 'node:url'
import type { IncomingMessage, ServerResponse } from 'node:http'
import type { Socket } from 'node:net'
import { defineConfig, type ConfigEnv, type ProxyOptions, type UserConfig } from 'vite'
import react from '@vitejs/plugin-react'

const LOCAL_FALLBACK = 'http://127.0.0.1:5421'

// Prefers HTTP endpoints over HTTPS and normalizes localhost to 127.0.0.1
// to avoid Node dual-stack connection issues.
function pickHttpFirst(raw: string): string | null {
  const urls = raw
    .split(/[;,\s]+/)
    .map((v) => v.trim())
    .filter(Boolean)

  const parsed: string[] = []
  for (const u of urls) {
    try {
      const p = new URL(u)
      if (p.hostname === 'localhost') p.hostname = '127.0.0.1'
      if (p.protocol === 'http:' || p.protocol === 'https:') {
        parsed.push(p.toString().replace(/\/$/, ''))
      }
    } catch {
      // Ignore malformed URLs.
    }
  }

  return parsed.find((v) => v.startsWith('http://')) ?? parsed[0] ?? null
}

function resolveLocalProxyTarget(): string {
  for (const key of ['VITE_API_PROXY_TARGET', 'SERVER_HTTP', 'ASPNETCORE_URLS', 'SERVER_HTTPS']) {
    const value = process.env[key]
    if (!value) {
      continue
    }

    const normalized = pickHttpFirst(value)
    if (normalized) {
      return normalized
    }
  }

  return LOCAL_FALLBACK
}

function isServerResponse(value: Socket | ServerResponse<IncomingMessage>): value is ServerResponse<IncomingMessage> {
  return 'writeHead' in value
}

const configureDevProxy: NonNullable<ProxyOptions['configure']> = (proxy) => {
  proxy.on('error', (err, _req, res) => {
    const code = (err as NodeJS.ErrnoException).code
    if (code === 'ECONNREFUSED' || code === 'ECONNRESET') {
      if (isServerResponse(res) && !res.headersSent) {
        res.writeHead(502, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify({ error: 'Backend not ready' }))
      }
      return
    }

    console.error(`[vite] proxy error: ${err.message}`)
  })
}

export default defineConfig((config: ConfigEnv): UserConfig => {
  const server = config.command === 'serve'
    ? {
      host: 'localhost',
      port: Number(process.env.PORT) || 5250,
      strictPort: true,
      proxy: {
        '^/api': {
          target: resolveLocalProxyTarget(),
          changeOrigin: true,
          secure: false,
          // Suppress noisy ECONNREFUSED errors during backend startup.
          // The browser receives a 502 and the frontend fetch layer can retry.
          configure: configureDevProxy,
        },
      },
    }
    : undefined

  return {
    base: '/',
    plugins: [react()],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    build: {
      rollupOptions: {
        output: {
          manualChunks(id: string) {
            const normalizedId = id.replace(/\\/g, '/')

            // Only split node_modules into manual chunks.
            // App code (src/) is left to Rollup's automatic splitting
            // to avoid circular chunk dependencies between tightly-coupled modules.
            if (!normalizedId.includes('node_modules')) {
              return
            }

            if (normalizedId.includes('@fluentui/react-icons')) {
              return 'fluentui-icons'
            }

            if (normalizedId.includes('@griffel')) {
              return 'griffel'
            }

            if (normalizedId.includes('@fluentui')) {
              return 'fluentui-components'
            }

            if (normalizedId.includes('@azure/msal')) {
              return 'msal'
            }

            if (normalizedId.includes('react-markdown') || normalizedId.includes('remark-gfm')) {
              return 'markdown'
            }

            if (normalizedId.includes('@tanstack/react-query')) {
              return 'query'
            }

            if (normalizedId.includes('react-router') || normalizedId.includes('react-dom') || /node_modules\/react\//.test(normalizedId) || normalizedId.includes('node_modules/scheduler')) {
              return 'react-core'
            }

            return 'vendor'
          },
        },
      },
    },
    server,
  }
})
