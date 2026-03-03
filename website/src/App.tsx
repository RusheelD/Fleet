import { BrowserRouter, Routes, Route } from 'react-router-dom'
import { SiteLayout } from './components'
import { HomePage, AboutPage, PricingPage, ContactPage } from './pages'

export function App() {
    return (
        <BrowserRouter>
            <Routes>
                <Route element={<SiteLayout />}>
                    <Route path="/" element={<HomePage />} />
                    <Route path="/about" element={<AboutPage />} />
                    <Route path="/pricing" element={<PricingPage />} />
                    <Route path="/contact" element={<ContactPage />} />
                </Route>
            </Routes>
        </BrowserRouter>
    )
}
