import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { FluentProvider } from '@fluentui/react-components'
import './index.css'
import { App } from './App'
import { warmDarkTheme, warmLightTheme } from './theme'

const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches

createRoot(document.getElementById('root')!).render(
    <StrictMode>
        <FluentProvider theme={prefersDark ? warmDarkTheme : warmLightTheme} style={{ minHeight: '100%' }}>
            <App />
        </FluentProvider>
    </StrictMode>,
)
