import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { RouterProvider } from 'react-router/dom'
import { LanguageProvider } from './i18n/LanguageProvider'
import { AuthProvider } from './app/auth/AuthProvider'
import { router } from './app/router'
import '@fontsource-variable/figtree'
import './index.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <LanguageProvider>
      <AuthProvider>
        <RouterProvider router={router} />
      </AuthProvider>
    </LanguageProvider>
  </StrictMode>,
)
