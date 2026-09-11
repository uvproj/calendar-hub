import { useEffect, useState, type FormEvent } from 'react'
import { ArrowLeft, KeyRound, UserPlus } from 'lucide-react'
import { ApiError, apiClient } from '../api/client'
import type { Member } from '../api/types'

interface AdminPanelProps { onClose(): void }

export function AdminPanel({ onClose }: AdminPanelProps) {
    const [members, setMembers] = useState<Member[]>([])
    const [message, setMessage] = useState('')
    const [error, setError] = useState('')
    const [resetMember, setResetMember] = useState<Member | null>(null)

    useEffect(() => { const controller = new AbortController(); apiClient.getMembers(controller.signal).then(setMembers).catch(showError); return () => controller.abort() }, [])
    function showError(caught: unknown) { setError(caught instanceof ApiError ? caught.message : 'The member action could not be completed.') }

    async function createMember(event: FormEvent<HTMLFormElement>) {
        event.preventDefault(); setError(''); setMessage(''); const form = event.currentTarget; const data = new FormData(form)
        try { const member = await apiClient.createMember({ username: String(data.get('username')), displayName: String(data.get('displayName')), passcode: String(data.get('passcode')), isAdministrator: data.get('isAdministrator') === 'on' }); setMembers((current) => [...current, member].sort((a, b) => a.username.localeCompare(b.username))); setMessage(`${member.displayName} can now sign in.`); form.reset() } catch (caught) { showError(caught) }
    }
    async function toggle(member: Member) { setError(''); setMessage(''); try { const updated = await apiClient.setMemberActive(member.id, !member.isActive); setMembers((current) => current.map((item) => item.id === updated.id ? updated : item)); setMessage(`${updated.displayName} is now ${updated.isActive ? 'enabled' : 'disabled'}.`) } catch (caught) { showError(caught) } }
    async function resetPasscode(event: FormEvent<HTMLFormElement>) { event.preventDefault(); if (!resetMember) return; const form = event.currentTarget; const data = new FormData(form); try { await apiClient.resetPasscode(resetMember.id, String(data.get('passcode'))); setMessage(`Passcode reset for ${resetMember.displayName}.`); setResetMember(null); form.reset() } catch (caught) { showError(caught) } }

    return <main className="admin-layout"><section className="admin-heading"><button className="text-button" type="button" onClick={onClose}><ArrowLeft aria-hidden="true" /> Back to calendar</button><p className="eyebrow">Administration</p><h1>Family members</h1><p className="muted">Accounts are local to this Calendar Hub host.</p></section>
        {message && <p className="success-banner" role="status">{message}</p>}{error && <p className="inline-error" role="alert">{error}</p>}
        <section className="member-list" aria-label="Member accounts">{members.map((member) => <article className="member-row" key={member.id}><div><strong>{member.displayName}</strong><span>@{member.username} · {member.isAdministrator ? 'Administrator' : 'Member'} · {member.isActive ? 'Active' : 'Disabled'}</span></div><div className="member-actions"><button type="button" className={member.isActive ? 'danger-button' : 'secondary-button'} onClick={() => toggle(member)}>{member.isActive ? `Disable ${member.displayName}` : `Enable ${member.displayName}`}</button><button className="icon-button" type="button" aria-label={`Reset passcode for ${member.displayName}`} title="Reset passcode" onClick={() => setResetMember(member)}><KeyRound aria-hidden="true" /></button></div></article>)}</section>
        <section className="member-form-section"><div className="section-heading"><UserPlus aria-hidden="true" /><div><p className="eyebrow">New account</p><h2>Add family member</h2></div></div><form className="member-form" onSubmit={createMember}><label>Display name<input name="displayName" required maxLength={100} /></label><label>Username<input name="username" required maxLength={100} autoComplete="off" /></label><label>Initial passcode<input name="passcode" type="password" required minLength={8} maxLength={128} autoComplete="new-password" /></label><label className="check-label"><input name="isAdministrator" type="checkbox" />Administrator</label><button className="primary-button" type="submit">Create member</button></form></section>
        {resetMember && <div className="dialog-backdrop"><section className="dialog" role="dialog" aria-modal="true" aria-labelledby="reset-title"><h2 id="reset-title">Reset {resetMember.displayName}'s passcode</h2><p>This signs out their existing sessions.</p><form onSubmit={resetPasscode}><label>New passcode<input name="passcode" type="password" required minLength={8} maxLength={128} autoComplete="new-password" /></label><div className="dialog-actions"><button className="secondary-button" type="button" onClick={() => setResetMember(null)}>Cancel</button><button className="danger-button" type="submit">Reset passcode</button></div></form></section></div>}
    </main>
}