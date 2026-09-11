import { useEffect, useState } from 'react'
import { CalendarDays, LogOut, Settings, Users } from 'lucide-react'
import { ApiError, apiClient } from './api/client'
import type { CurrentUser } from './api/types'
import { AdminPanel } from './components/AdminPanel'
import { AuthView } from './components/AuthView'
import { CalendarWorkspace } from './components/CalendarWorkspace'
import './App.css'

type Screen = 'calendar' | 'members'

function App() {
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [checkingSession, setCheckingSession] = useState(true)
  const [screen, setScreen] = useState<Screen>('calendar')

  useEffect(() => {
    const controller = new AbortController()
    apiClient.getCurrentUser(controller.signal)
      .then(setUser)
      .catch((error: unknown) => {
        if (!(error instanceof ApiError && error.status === 401) && !(error instanceof DOMException && error.name === 'AbortError')) {
          console.error(error)
        }
      })
      .finally(() => setCheckingSession(false))
    return () => controller.abort()
  }, [])

  if (checkingSession) {
    return <main className="session-loading" aria-live="polite">Opening your calendar...</main>
  }

  if (!user) {
    return <AuthView onAuthenticated={setUser} />
  }

  async function logout() {
    await apiClient.logout()
    setUser(null)
    setScreen('calendar')
  }

  return (
    <div className="app-shell">
      <header className="app-header">
        <button className="brand-button" type="button" onClick={() => setScreen('calendar')} aria-label="Open calendar">
          <span className="brand-mark"><CalendarDays aria-hidden="true" /></span>
          <span><strong>Common Ground</strong><small>Family calendar</small></span>
        </button>
        <nav aria-label="Account navigation">
          <span className="current-user">{user.displayName}</span>
          {user.isAdministrator && (
            <button className="icon-button" type="button" aria-label="Manage members" title="Manage members" onClick={() => setScreen('members')}><Users aria-hidden="true" /></button>
          )}
          <button className="icon-button" type="button" aria-label="Calendar settings" title="Calendar settings" disabled><Settings aria-hidden="true" /></button>
          <button className="icon-button" type="button" aria-label="Sign out" title="Sign out" onClick={logout}><LogOut aria-hidden="true" /></button>
        </nav>
      </header>
      {screen === 'members' && user.isAdministrator
        ? <AdminPanel onClose={() => setScreen('calendar')} />
        : <CalendarWorkspace />}
    </div>
  )
}

export default App
