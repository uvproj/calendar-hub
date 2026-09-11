import { useState, type FormEvent } from 'react'
import { CalendarPlus, RotateCcw, Trash2 } from 'lucide-react'
import { ApiError, apiClient } from '../api/client'
import type { BatchCreationItem, CalendarDraft, CalendarSummary } from '../api/types'
import { inputToOffsetIso, isoToInput } from '../calendar/dateTime'

interface EventComposerProps { calendars: CalendarSummary[]; onBatchComplete(): void }
type EditableDraft = Omit<CalendarDraft, 'invitees'> & { invitees: string }

function editable(draft: CalendarDraft): EditableDraft {
    return { ...draft, startsAt: isoToInput(draft.startsAt, draft.isAllDay), endsAt: isoToInput(draft.endsAt, draft.isAllDay), invitees: draft.invitees.join(', ') }
}

function validateDraft(draft: EditableDraft): string[] {
    const errors: string[] = []
    if (!draft.name.trim()) errors.push('Name is required.')
    if (!draft.startsAt || !draft.endsAt) errors.push('Start and end are required.')
    if (draft.startsAt && draft.endsAt && draft.endsAt <= draft.startsAt) errors.push('End must be after start.')
    const emails = draft.invitees.split(',').map((value) => value.trim()).filter(Boolean)
    if (emails.some((email) => !/^\S+@\S+\.\S+$/.test(email))) errors.push('Invitees must be valid email addresses.')
    return errors
}

export function EventComposer({ calendars, onBatchComplete }: EventComposerProps) {
    const [destination, setDestination] = useState('')
    const [text, setText] = useState('')
    const [drafts, setDrafts] = useState<EditableDraft[]>([])
    const [warnings, setWarnings] = useState<string[]>([])
    const [clarifications, setClarifications] = useState<string[]>([])
    const [results, setResults] = useState<BatchCreationItem[]>([])
    const [error, setError] = useState('')
    const [busy, setBusy] = useState(false)
    const selectedDestination = destination || calendars.find((calendar) => calendar.isDefault)?.name || calendars[0]?.name || ''

    async function interpret(event: FormEvent) {
        event.preventDefault(); setBusy(true); setError(''); setResults([])
        try {
            const response = await apiClient.interpret(text, selectedDestination)
            setDrafts(response.drafts.map(editable)); setWarnings(response.warnings); setClarifications(response.clarificationErrors)
        } catch (caught) { setError(caught instanceof ApiError ? caught.message : 'The events could not be interpreted.') }
        finally { setBusy(false) }
    }

    function update(index: number, field: keyof EditableDraft, value: string | boolean) {
        setDrafts((current) => current.map((draft, draftIndex) => draftIndex === index ? { ...draft, [field]: value } : draft))
    }

    async function confirm() {
        if (drafts.some((draft) => validateDraft(draft).length > 0)) return
        setBusy(true); setError('')
        try {
            const response = await apiClient.createBatch(selectedDestination, drafts.map((draft) => ({
                ...draft, description: draft.description?.trim() || null, location: draft.location?.trim() || null,
                startsAt: inputToOffsetIso(draft.startsAt, draft.isAllDay), endsAt: inputToOffsetIso(draft.endsAt, draft.isAllDay),
                invitees: draft.invitees.split(',').map((value) => value.trim()).filter(Boolean),
            })))
            setResults(response.items); onBatchComplete()
        } catch (caught) { setError(caught instanceof ApiError ? caught.message : 'The batch could not be created.') }
        finally { setBusy(false) }
    }

    function reset() { setDrafts([]); setWarnings([]); setClarifications([]); setResults([]); setError('') }

    return <aside className="composer" aria-labelledby="composer-title">
        <div className="section-heading"><CalendarPlus aria-hidden="true" /><div><p className="eyebrow">Plan with text</p><h2 id="composer-title">Add events</h2></div></div>
        <form onSubmit={interpret} className="composer-form">
            <label>Destination calendar<select value={selectedDestination} onChange={(event) => setDestination(event.target.value)} required>{calendars.map((calendar) => <option key={calendar.name}>{calendar.name}</option>)}</select></label>
            <label>Describe events<textarea value={text} onChange={(event) => setText(event.target.value)} rows={4} maxLength={4000} placeholder="Dinner Friday at 6, then a picnic all day Saturday" required /></label>
            <button className="primary-button" type="submit" disabled={busy || !selectedDestination}>{busy ? 'Working...' : drafts.length ? 'Interpret again' : 'Interpret'}</button>
        </form>
        {warnings.map((warning) => <p className="notice warning" key={warning}>{warning}</p>)}
        {clarifications.map((message) => <p className="notice clarification" key={message}>{message}</p>)}
        {error && <p className="inline-error" role="alert">{error}</p>}
        {drafts.length > 0 && <div className="drafts"><div className="drafts-heading"><h3>Review {drafts.length} {drafts.length === 1 ? 'event' : 'events'}</h3><button className="icon-button" type="button" aria-label="Discard preview" title="Discard preview" onClick={reset}><RotateCcw aria-hidden="true" /></button></div>
            {drafts.map((draft, index) => {
                const rowErrors = validateDraft(draft)
                return <fieldset className="draft-row" data-testid="draft-row" key={index}><legend>Event {index + 1}</legend><button className="icon-button remove-draft" type="button" aria-label={`Discard event ${index + 1}`} title="Discard event" onClick={() => setDrafts((current) => current.filter((_, itemIndex) => itemIndex !== index))}><Trash2 aria-hidden="true" /></button>
                    <label>Name<input value={draft.name} onChange={(event) => update(index, 'name', event.target.value)} maxLength={200} /></label><label>Description<textarea value={draft.description ?? ''} onChange={(event) => update(index, 'description', event.target.value)} rows={2} maxLength={4000} /></label><label>Location<input value={draft.location ?? ''} onChange={(event) => update(index, 'location', event.target.value)} maxLength={500} /></label>
                    <label className="check-label"><input type="checkbox" checked={draft.isAllDay} onChange={(event) => update(index, 'isAllDay', event.target.checked)} />All day</label><div className="date-grid"><label>Starts<input type={draft.isAllDay ? 'date' : 'datetime-local'} value={draft.startsAt} onChange={(event) => update(index, 'startsAt', event.target.value)} /></label><label>Ends <small>{draft.isAllDay ? '(exclusive)' : ''}</small><input type={draft.isAllDay ? 'date' : 'datetime-local'} value={draft.endsAt} onChange={(event) => update(index, 'endsAt', event.target.value)} /></label></div><label>Invitees <small>(comma separated)</small><input value={draft.invitees} onChange={(event) => update(index, 'invitees', event.target.value)} /></label>
                    {rowErrors.length > 0 && <ul className="field-errors">{rowErrors.map((rowError) => <li key={rowError}>{rowError}</li>)}</ul>}
                </fieldset>
            })}
            <button className="confirm-button" type="button" disabled={busy || drafts.some((draft) => validateDraft(draft).length > 0)} onClick={confirm}>Confirm {drafts.length} {drafts.length === 1 ? 'event' : 'events'}</button></div>}
        {results.length > 0 && <div className="batch-results" aria-live="polite"><h3>Creation results</h3>{[...results].sort((a, b) => a.index - b.index).map((result) => result.event ? <p className="result-success" key={result.index}>Created: {result.event.name}</p> : <p className="result-failure" key={result.index}>Failed: {result.errorMessage}</p>)}</div>}
    </aside>
}