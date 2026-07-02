import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { hydratePersistentStorage } from './lib/persistentStorage'

// On the native (Capacitor) shell, restore tokens/identity from durable
// storage before anything renders — AuthProvider reads them on mount.
// On web this resolves immediately.
void hydratePersistentStorage().finally(() => {
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <App />
    </StrictMode>,
  )
})
