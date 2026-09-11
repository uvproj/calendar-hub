import { describe, expect, it } from 'vitest'
import { calendarColor, mapApiEvent } from './eventMapping'

describe('calendar event mapping', () => {
  it('maps timed offsets and all-day exclusive boundaries for FullCalendar', () => {
    const timed = mapApiEvent({
      sourceCalendarName: 'Family', sourceCalendarType: 'Google',
      identity: { calendarName: 'Family', providerEventId: 'timed-1' },
      name: 'Dinner', description: null, location: null,
      startsAt: '2026-09-11T18:00:00+05:30', endsAt: '2026-09-11T19:00:00+05:30',
      isAllDay: false, invitees: [],
    })
    const allDay = mapApiEvent({
      sourceCalendarName: 'School', sourceCalendarType: 'FileSystem',
      identity: { calendarName: 'School', providerEventId: 'all-day-1' },
      name: 'Holiday', description: null, location: null,
      startsAt: '2026-09-12T00:00:00+05:30', endsAt: '2026-09-14T00:00:00+05:30',
      isAllDay: true, invitees: [],
    })

    expect(timed).toMatchObject({ start: '2026-09-11T18:00:00+05:30', end: '2026-09-11T19:00:00+05:30', allDay: false })
    expect(allDay).toMatchObject({ start: '2026-09-12', end: '2026-09-14', allDay: true })
  })

  it('assigns deterministic accessible source colors', () => {
    expect(calendarColor('Family', 'Google')).toEqual(calendarColor('Family', 'Google'))
    expect(calendarColor('Family', 'Google')).not.toEqual(calendarColor('School', 'FileSystem'))
  })
})