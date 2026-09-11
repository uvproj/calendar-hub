import { useState, type FormEvent } from 'react'
import { ArrowLeft, CalendarDays } from 'lucide-react'
import { ApiError, apiClient } from '../api/client'
import type { CurrentUser } from '../api/types'

interface AuthViewProps {
    onAuthenticated(user: CurrentUser): void
}

export function AuthView({ onAuthenticated }: AuthViewProps) {
    const [setupMode, setSetupMode] = useState(false)
    const [busy, setBusy] = useState(false)
    const [error, setError] = useState('')

    async function submit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault()
        setBusy(true)
        setError('')
        const form = event.currentTarget
        const data = new FormData(form)
        try {
            const authenticatedUser = setupMode
                ? await apiClient.bootstrap({
                    username: String(data.get('username') ?? ''),
                    displayName: String(data.get('displayName') ?? ''),
                    passcode: String(data.get('passcode') ?? ''),
                    bootstrapSecret: String(data.get('bootstrapSecret') ?? ''),
                })
                : await apiClient.login({
                    username: String(data.get('username') ?? ''),
                    passcode: String(data.get('passcode') ?? ''),
                })
            form.reset()
            onAuthenticated(authenticatedUser)
        } catch (caught) {
            setError(caught instanceof ApiError ? caught.message : 'Sign-in could not be completed.')
        } finally {
            setBusy(false)
        }
    }

    return (
        <main className="auth-layout">
            <section className="auth-context" aria-label="Calendar overview">
                <CalendarDays aria-hidden="true" />
                <p>One shared place for plans, school days, appointments, and the small things that hold a week together.</p>
            </section>
            <section className="auth-panel">
                {setupMode && (
                    <button className="text-button back-button" type="button" onClick={() => { setSetupMode(false); setError('') }}>
                        <ArrowLeft aria-hidden="true" /> Back to sign in
                    </button>
                )}
                <p className="eyebrow">Common Ground</p>
                <h1>{setupMode ? 'Set up your family calendar' : 'Family calendar'}</h1>
                <p className="muted">{setupMode ? 'Create the first administrator account on this host.' : 'Sign in with your local family account.'}</p>
                <form className="auth-form" onSubmit={submit}>
                    {setupMode && <label>Display name<input name="displayName" autoComplete="name" required maxLength={100} /></label>}
                    <label>Username<input name="username" autoComplete="username" required maxLength={100} /></label>
                    <label>Passcode<input name="passcode" type="password" autoComplete={setupMode ? 'new-password' : 'current-password'} required minLength={8} maxLength={128} /></label>
                    {setupMode && <label>One-time setup secret<input name="bootstrapSecret" type="password" autoComplete="off" required /></label>}
                    {error && <p className="inline-error" role="alert">{error}</p>}
                    <button className="primary-button" type="submit" disabled={busy}>{busy ? 'Working...' : setupMode ? 'Create administrator' : 'Sign in'}</button>
                </form>
                {!setupMode && <button className="text-button setup-link" type="button" onClick={() => { setSetupMode(true); setError('') }}>First-time setup</button>}
            </section>
        </main>
    )
}