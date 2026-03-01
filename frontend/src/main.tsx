import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { FluentProvider, webDarkTheme, webLightTheme } from '@fluentui/react-components'
import './index.css'
import App from './App.tsx'

const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <FluentProvider theme={prefersDark ? webDarkTheme : webLightTheme}>
      <App />
    </FluentProvider>
  </StrictMode>,
)
