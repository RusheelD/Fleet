import { fileURLToPath, URL } from 'node:url'
import { defineConfig, type ConfigEnv, type UserConfig } from 'vite'
import react from '@vitejs/plugin-react'

// ── Remote API targets ─────────────────────────────────────────
// Add deployment URLs here and select them with `vite --mode <name>`.
// Any mode not in this map falls through to local Aspire env-var resolution.
const remoteApiTargets = new Map<string, string>([
  // ['staging',    'https://fleet-staging.azurewebsites.net/'],
  // ['production', 'https://fleet.azurewebsites.net/'],
])

const LOCAL_FALLBACK = 'http://127.0.0.1:5421'

// ── Aspire env-var resolution ──────────────────────────────────
// Prefers HTTP endpoints over HTTPS, normalises "localhost" → 127.0.0.1
// to prevent Node dual-stack (IPv6 + IPv4) connection failures.

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
      if (p.protocol === 'http:' || p.protocol === 'https:')
        parsed.push(p.toString().replace(/\/$/, ''))
    } catch { /* skip malformed URLs */ }
  }
  return parsed.find((v) => v.startsWith('http://')) ?? parsed[0] ?? null
}

function resolveLocalTarget(): string {
  for (const key of [
    'VITE_API_PROXY_TARGET',
    'SERVER_HTTP',
    'ASPNETCORE_URLS',
    'SERVER_HTTPS',
  ]) {
    const val = process.env[key]
    if (val) {
      const norm = pickHttpFirst(val)
      if (norm) return norm
    }
  }
  return LOCAL_FALLBACK
}

// ── Vite configuration ────────────────────────────────────────
export default defineConfig((config: ConfigEnv): UserConfig => {
  // Use `vite --mode staging` (etc.) to hit a remote target instead of local
  const apiTarget = remoteApiTargets.get(config.mode) ?? resolveLocalTarget()

  console.log(`[vite] mode: "${config.mode}" → API target: ${apiTarget}`)

  return {
    base: '/',
    plugins: [react()],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    server: {
      host: 'localhost',
      port: Number(process.env.PORT) || 5250,
      strictPort: true,
      proxy: {
        '^/api': {
          target: apiTarget,
          changeOrigin: true,
          secure: false,
          // Suppress noisy ECONNREFUSED errors during backend startup.
          // The browser receives a 502 and the frontend's fetch layer can retry.
          configure: (proxy) => {
            proxy.on('error', (err, _req, res) => {
              const code = (err as NodeJS.ErrnoException).code
              if (code === 'ECONNREFUSED' || code === 'ECONNRESET') {
                if (res && 'writeHead' in res && !res.headersSent) {
                  res.writeHead(502, { 'Content-Type': 'application/json' })
                  res.end(JSON.stringify({ error: 'Backend not ready' }))
                }
                return
              }
              console.error(`[vite] proxy error: ${err.message}`)
            })
          },
        },
      },
    },
  }
})
