import { useCallback, useEffect, useRef, useState } from 'react'
import FullCalendar from '@fullcalendar/react'
import dayGridPlugin from '@fullcalendar/daygrid'
import timeGridPlugin from '@fullcalendar/timegrid'
import interactionPlugin from '@fullcalendar/interaction'
import type { DatesSetArg, EventClickArg } from '@fullcalendar/core'
import { ChevronLeft, ChevronRight, RefreshCw } from 'lucide-react'
import { ApiError, apiClient } from '../api/client'
import type { CalendarEvent, CalendarQueryError, CalendarSummary } from '../api/types'
import { calendarColor, mapApiEvent } from '../calendar/eventMapping'
import { EventComposer } from './EventComposer'

type CalendarView = 'dayGridMonth' | 'timeGridWeek' | 'timeGridDay'
const validViews: CalendarView[] = ['dayGridMonth', 'timeGridWeek', 'timeGridDay']

function storedView(): CalendarView {
    const value = localStorage.getItem('calendar-hub-view') as CalendarView | null
    return value && validViews.includes(value) ? value : 'dayGridMonth'
}

export function CalendarWorkspace() {
    const calendarRef = useRef<FullCalendar>(null)
    const [calendars, setCalendars] = useState<CalendarSummary[]>([])
    const [selected, setSelected] = useState<string[]>([])
    const [events, setEvents] = useState<CalendarEvent[]>([])
    const [errors, setErrors] = useState<CalendarQueryError[]>([])
    const [range, setRange] = useState<{ start: string; end: string } | null>(null)
    const [view, setView] = useState<CalendarView>(storedView)
    const [title, setTitle] = useState('Calendar')
    const [loading, setLoading] = useState(true)
    const [loadError, setLoadError] = useState('')
    const [selectedEvent, setSelectedEvent] = useState<CalendarEvent | null>(null)
    const [refreshKey, setRefreshKey] = useState(0)

    useEffect(() => {
        const controller = new AbortController()
        apiClient.getCalendars(controller.signal)
            .then((items) => {
                setCalendars(items)
                const saved = JSON.parse(localStorage.getItem('calendar-hub-filters') ?? '[]') as string[]
                const availableSaved = saved.filter((name) => items.some((calendar) => calendar.name === name))
                setSelected(availableSaved.length ? availableSaved : items.map((calendar) => calendar.name))
            })
            .catch((error: unknown) => setLoadError(error instanceof ApiError ? error.message : 'Calendars could not be loaded.'))
            .finally(() => setLoading(false))
        return () => controller.abort()
    }, [])

    useEffect(() => {
        if (!range || selected.length === 0) return
        const controller = new AbortController()
        setLoading(true)
        setLoadError('')
        apiClient.getEvents(range.start, range.end, selected, controller.signal)
            .then((response) => {
                setEvents(response.events)
                setErrors(response.errors)
            })
            .catch((error: unknown) => {
                if (!(error instanceof DOMException && error.name === 'AbortError')) {
                    setLoadError(error instanceof ApiError ? error.message : 'Events could not be loaded.')
                }
            })
            .finally(() => setLoading(false))
        return () => controller.abort()
    }, [range, selected, refreshKey])

    const datesSet = useCallback((dateRange: DatesSetArg | { startStr: string; endStr: string }) => {
        setRange({ start: dateRange.startStr, end: dateRange.endStr })
        if ('view' in dateRange) {
            setTitle(dateRange.view.title)
            localStorage.setItem('calendar-hub-date', dateRange.view.currentStart.toISOString())
        }
    }, [])

    function chooseView(nextView: CalendarView) {
        setView(nextView)
        localStorage.setItem('calendar-hub-view', nextView)
        calendarRef.current?.getApi().changeView(nextView)
    }

    function toggleCalendar(name: string) {
        setSelected((current) => {
            const next = current.includes(name) ? current.filter((item) => item !== name) : [...current, name]
            localStorage.setItem('calendar-hub-filters', JSON.stringify(next))
            return next
        })
    }

    function navigate(action: 'prev' | 'next' | 'today') {
        calendarRef.current?.getApi()[action]()
    }

    function openEvent(info: EventClickArg) {
        setSelectedEvent(events.find((event) => `${event.identity.calendarName}:${event.identity.providerEventId}` === info.event.id) ?? null)
    }

    return (
        <main className="workspace">
            <section className="calendar-panel" aria-labelledby="calendar-title">
                <div className="calendar-heading">
                    <div><p className="eyebrow">Shared schedule</p><h1 id="calendar-title">{title}</h1></div>
                    <div className="calendar-navigation" aria-label="Calendar navigation">
                        <button className="icon-button" type="button" aria-label="Previous period" title="Previous period" onClick={() => navigate('prev')}><ChevronLeft aria-hidden="true" /></button>
                        <button className="today-button" type="button" onClick={() => navigate('today')}>Today</button>
                        <button className="icon-button" type="button" aria-label="Next period" title="Next period" onClick={() => navigate('next')}><ChevronRight aria-hidden="true" /></button>
                    </div>
                </div>
                <div className="view-switcher" aria-label="Calendar view">
                    <button type="button" aria-label="Month view" aria-pressed={view === 'dayGridMonth'} onClick={() => chooseView('dayGridMonth')}>Month</button>
                    <button type="button" aria-label="Week view" aria-pressed={view === 'timeGridWeek'} onClick={() => chooseView('timeGridWeek')}>Week</button>
                    <button type="button" aria-label="Day view" aria-pressed={view === 'timeGridDay'} onClick={() => chooseView('timeGridDay')}>Day</button>
                </div>
                <fieldset className="calendar-filters">
                    <legend>Calendars</legend>
                    {calendars.map((calendar) => {
                        const color = calendarColor(calendar.name, calendar.type)
                        return <label key={calendar.name}>
                            <input type="checkbox" checked={selected.includes(calendar.name)} onChange={() => toggleCalendar(calendar.name)} />
                            <span className="calendar-swatch" style={{ backgroundColor: color.background, borderColor: color.border }} aria-hidden="true" />
                            {calendar.name}<small>{calendar.type}</small>
                        </label>
                    })}
                </fieldset>
                {selected.length === 0 ? <div className="calendar-state">Select at least one calendar to load events.</div> : (
                    <div className="calendar-frame" aria-busy={loading}>
                        <FullCalendar ref={calendarRef} plugins={[dayGridPlugin, timeGridPlugin, interactionPlugin]} initialView={view}
                            initialDate={localStorage.getItem('calendar-hub-date') ?? undefined} headerToolbar={false} height="100%" nowIndicator navLinks dayMaxEvents={3}
                            eventTimeFormat={{ hour: 'numeric', minute: '2-digit', meridiem: 'short' }} events={events.map(mapApiEvent)} datesSet={datesSet} eventClick={openEvent} />
                    </div>
                )}
                {loading && <p className="status-line" aria-live="polite"><RefreshCw className="spin" aria-hidden="true" /> Loading calendar...</p>}
                {loadError && <p className="inline-error" role="alert">{loadError}</p>}
                {!loading && selected.length > 0 && events.length === 0 && !loadError && <p className="status-line">No events in this range.</p>}
                {errors.length > 0 && <div className="partial-errors" role="status">{errors.map((error) => <p key={error.calendarName}>{error.calendarName}: {error.message}</p>)}</div>}
                <div className="event-index" aria-label="Events in range">{events.map((event) => <button type="button" key={`${event.identity.calendarName}:${event.identity.providerEventId}`} onClick={() => setSelectedEvent(event)}><strong>{event.name}</strong><span>{event.sourceCalendarName}</span></button>)}</div>
                {selectedEvent && <aside className="event-detail" aria-label="Event details"><button className="text-button" type="button" onClick={() => setSelectedEvent(null)}>Close details</button><p className="eyebrow">{selectedEvent.sourceCalendarName}</p><h2>{selectedEvent.name}</h2><p>{selectedEvent.isAllDay ? 'All day' : new Date(selectedEvent.startsAt).toLocaleString()}</p>{selectedEvent.location && <p>{selectedEvent.location}</p>}{selectedEvent.description && <p>{selectedEvent.description}</p>}</aside>}
            </section>
            <EventComposer calendars={calendars} onBatchComplete={() => setRefreshKey((value) => value + 1)} />
        </main>
    )
}