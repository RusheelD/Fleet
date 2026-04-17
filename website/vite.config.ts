import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  base: '/',
  plugins: [react()],
  build: {
    rollupOptions: {
      output: {
        manualChunks(id: string) {
          const normalizedId = id.replace(/\\/g, '/')

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

          if (normalizedId.includes('react-router')) {
            return 'router'
          }

          if (normalizedId.includes('react-dom') || /node_modules\/react\//.test(normalizedId) || normalizedId.includes('node_modules/scheduler')) {
            return 'react-core'
          }

          return 'vendor'
        },
      },
    },
  },
  server: {
    port: Number(process.env.PORT) || 5251,
    strictPort: true,
  },
})
