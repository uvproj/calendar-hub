import type { EventInput } from '@fullcalendar/core'
import type { CalendarEvent } from '../api/types'

export interface CalendarColor {
    background: string
    border: string
    text: string
}

const palette: CalendarColor[] = [
    { background: '#dcefe5', border: '#287052', text: '#153c2d' },
    { background: '#f8dfbf', border: '#9a541e', text: '#59300f' },
    { background: '#d9e9f3', border: '#316987', text: '#19394b' },
    { background: '#f2dce2', border: '#9a4861', text: '#552536' },
    { background: '#e7e1f0', border: '#66518b', text: '#392d50' },
    { background: '#e6e7cf', border: '#6d7329', text: '#3d4017' },
]

export function calendarColor(name: string, type: string): CalendarColor {
    let hash = 2166136261
    for (const character of `${type}:${name}`.toLocaleLowerCase()) {
        hash ^= character.charCodeAt(0)
        hash = Math.imul(hash, 16777619)
    }
    return palette[Math.abs(hash) % palette.length]
}

export function mapApiEvent(event: CalendarEvent): EventInput {
    const color = calendarColor(event.sourceCalendarName, event.sourceCalendarType)
    return {
        id: `${event.identity.calendarName}:${event.identity.providerEventId}`,
        title: event.name,
        start: event.isAllDay ? event.startsAt.slice(0, 10) : event.startsAt,
        end: event.isAllDay ? event.endsAt.slice(0, 10) : event.endsAt,
        allDay: event.isAllDay,
        backgroundColor: color.background,
        borderColor: color.border,
        textColor: color.text,
        extendedProps: {
            sourceCalendarName: event.sourceCalendarName,
            sourceCalendarType: event.sourceCalendarType,
            description: event.description,
            location: event.location,
            invitees: event.invitees,
        },
    }
}