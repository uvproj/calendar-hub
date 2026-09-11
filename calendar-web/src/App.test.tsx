import { useEffect } from 'react'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { axe } from 'jest-axe'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'

vi.mock('@fullcalendar/react', () => ({
    default: function FullCalendarMock({ datesSet, events }: { datesSet?: (range: { startStr: string; endStr: string }) => void; events?: unknown[] }) {
        useEffect(() => datesSet?.({ startStr: '2026-09-01T00:00:00Z', endStr: '2026-10-01T00:00:00Z' }), [datesSet])
        return <div data-testid="full-calendar">{events?.length ?? 0} events</div>
    },
}))

const userResponse = { id: 'u1', username: 'alex', displayName: 'Alex', isAdministrator: true }
const calendarsResponse = [
    { name: 'Family', type: 'Google', isDefault: true },
    { name: 'School', type: 'FileSystem', isDefault: false },
]

function json(body: unknown, status = 200) {
    return Promise.resolve(new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }))
}

function installFetchMock(authenticated = true) {
    return vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => {
        const url = String(input)
        if (url === '/api/auth/me') return authenticated ? json(userResponse) : json({ title: 'Unauthorized' }, 401)
        if (url === '/api/auth/antiforgery') return json({ requestToken: 'csrf', headerName: 'X-CSRF-TOKEN' })
        if (url === '/api/auth/login') return json(userResponse)
        if (url === '/api/calendars') return json(calendarsResponse)
        if (url.startsWith('/api/events?')) return json({
            events: [{
                sourceCalendarName: 'Family', sourceCalendarType: 'Google',
                identity: { calendarName: 'Family', providerEventId: '1' }, name: 'Dinner',
                description: null, location: 'Kitchen', startsAt: '2026-09-11T18:00:00Z',
                endsAt: '2026-09-11T19:00:00Z', isAllDay: false, invitees: [],
            }],
            errors: [{ calendarName: 'School', calendarType: 'FileSystem', category: 'Unavailable', message: 'Calendar is temporarily unavailable.' }],
        })
        if (url === '/api/calendar-interpretations') return json({
            destinationCalendar: 'Family', warnings: ['Check travel time.'], clarificationErrors: [],
            drafts: [
                { name: 'Dinner', description: '', location: 'Kitchen', startsAt: '2026-09-11T18:00:00Z', endsAt: '2026-09-11T19:00:00Z', isAllDay: false, invitees: [] },
                { name: 'Picnic', description: '', location: 'Park', startsAt: '2026-09-12T00:00:00Z', endsAt: '2026-09-13T00:00:00Z', isAllDay: true, invitees: [] },
            ],
        })
        if (url === '/api/events/batch') return json({
            destinationCalendarName: 'Family', destinationCalendarType: 'Google',
            items: [
                { index: 0, event: { name: 'Updated dinner' }, errorCategory: null, errorMessage: null },
                { index: 1, event: null, errorCategory: 'Provider', errorMessage: 'Picnic could not be created.' },
            ],
        })
        if (url === '/api/admin/members/') return json([{ id: 'u1', username: 'alex', displayName: 'Alex', isActive: true, isAdministrator: true }])
        if (url === '/api/admin/members' && init?.method === 'POST') return json({ id: 'u2', username: 'sam', displayName: 'Sam', isActive: true, isAdministrator: false }, 201)
        if (url.includes('/active')) return json({ id: 'u1', username: 'alex', displayName: 'Alex', isActive: false, isAdministrator: true })
        if (url.includes('/passcode')) return Promise.resolve(new Response(null, { status: 204 }))
        return json({ title: `Unexpected request: ${url}` }, 500)
    })
}

describe('Calendar Hub app', () => {
    beforeEach(() => {
        localStorage.clear()
        vi.restoreAllMocks()
    })

    it('shows login, authenticates, and exposes setup without persisting credentials', async () => {
        const fetchMock = installFetchMock(false)
        const user = userEvent.setup()
        render(<App />)

        expect(await screen.findByRole('heading', { name: 'Family calendar' })).toBeInTheDocument()
        await user.type(screen.getByLabelText('Username'), 'alex')
        await user.type(screen.getByLabelText('Passcode'), 'family-passcode')
        await user.click(screen.getByRole('button', { name: 'Sign in' }))

        expect(await screen.findByText('Alex')).toBeInTheDocument()
        expect(fetchMock).toHaveBeenCalledWith('/api/auth/login', expect.objectContaining({ method: 'POST' }))
        expect(JSON.stringify(localStorage)).not.toContain('family-passcode')
    })

    it('switches views, requests the active range with repeated filters, and shows partial errors', async () => {
        const fetchMock = installFetchMock()
        const user = userEvent.setup()
        render(<App />)

        await screen.findByTestId('full-calendar')
        await user.click(screen.getByRole('button', { name: 'Week view' }))
        expect(screen.getByRole('button', { name: 'Week view' })).toHaveAttribute('aria-pressed', 'true')
        expect(localStorage.getItem('calendar-hub-view')).toBe('timeGridWeek')
        expect(fetchMock.mock.calls.some(([url]) => String(url).includes('calendar=Family') && String(url).includes('calendar=School'))).toBe(true)
        expect(await screen.findByText('School: Calendar is temporarily unavailable.')).toBeInTheDocument()

        await user.click(screen.getByRole('checkbox', { name: /Family/ }))
        await user.click(screen.getByRole('checkbox', { name: /School/ }))
        expect(screen.getByText('Select at least one calendar to load events.')).toBeInTheDocument()
    })

    it('previews and edits multiple drafts without creating until explicit confirmation', async () => {
        const fetchMock = installFetchMock()
        const user = userEvent.setup()
        render(<App />)

        await screen.findByTestId('full-calendar')
        await user.type(screen.getByLabelText('Describe events'), 'Dinner and picnic')
        await user.click(screen.getByRole('button', { name: 'Interpret' }))

        expect(await screen.findByText('Check travel time.')).toBeInTheDocument()
        expect(fetchMock.mock.calls.filter(([url]) => url === '/api/events/batch')).toHaveLength(0)
        const rows = screen.getAllByTestId('draft-row')
        expect(rows).toHaveLength(2)
        const name = within(rows[0]).getByLabelText('Name')
        await user.clear(name)
        await user.type(name, 'Updated dinner')
        await user.click(screen.getByRole('button', { name: 'Confirm 2 events' }))

        expect(await screen.findByText('Created: Updated dinner')).toBeInTheDocument()
        expect(screen.getByText('Failed: Picnic could not be created.')).toBeInTheDocument()
        expect(fetchMock.mock.calls.filter(([url]) => url === '/api/events/batch')).toHaveLength(1)
    })

    it('shows administrator navigation and performs explicit member actions', async () => {
        installFetchMock()
        const user = userEvent.setup()
        render(<App />)

        await user.click(await screen.findByRole('button', { name: 'Manage members' }))
        expect(await screen.findByRole('heading', { name: 'Family members' })).toBeInTheDocument()
        await user.click(screen.getByRole('button', { name: 'Disable Alex' }))
        expect(await screen.findByText('Alex is now disabled.')).toBeInTheDocument()
    })

    it('has no obvious accessibility violations in login and calendar views', async () => {
        installFetchMock(false)
        const login = render(<App />)
        await screen.findByRole('button', { name: 'Sign in' })
        expect((await axe(login.container)).violations).toHaveLength(0)
        login.unmount()

        installFetchMock(true)
        const calendar = render(<App />)
        await screen.findByTestId('full-calendar')
        await waitFor(() => expect(screen.getByText('Dinner')).toBeInTheDocument())
        expect((await axe(calendar.container)).violations).toHaveLength(0)
    })
})