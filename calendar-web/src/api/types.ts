export interface AntiforgeryToken {
    requestToken: string
    headerName: string
}

export interface CurrentUser {
    id: string
    username: string
    displayName: string
    isAdministrator: boolean
}

export interface CalendarSummary {
    name: string
    type: string
    isDefault: boolean
}

export interface CalendarEventIdentity {
    calendarName: string
    providerEventId: string
}

export interface CalendarEvent {
    sourceCalendarName: string
    sourceCalendarType: string
    identity: CalendarEventIdentity
    name: string
    description: string | null
    location: string | null
    startsAt: string
    endsAt: string
    isAllDay: boolean
    invitees: string[]
}

export interface CalendarQueryError {
    calendarName: string
    calendarType: string
    category: string
    message: string
}

export interface AggregateEventsResponse {
    events: CalendarEvent[]
    errors: CalendarQueryError[]
}

export interface CalendarDraft {
    name: string
    description: string | null
    location: string | null
    startsAt: string
    endsAt: string
    isAllDay: boolean
    invitees: string[]
}

export interface CalendarInterpretationResponse {
    destinationCalendar: string
    drafts: CalendarDraft[]
    warnings: string[]
    clarificationErrors: string[]
}

export interface BatchCreationItem {
    index: number
    event: CalendarEvent | null
    errorCategory: string | null
    errorMessage: string | null
}

export interface BatchCreationResponse {
    destinationCalendarName: string
    destinationCalendarType: string
    items: BatchCreationItem[]
}

export interface Member {
    id: string
    username: string
    displayName: string
    isActive: boolean
    isAdministrator: boolean
}

export interface ProblemDetails {
    title?: string
    detail?: string
    status?: number
    errors?: Record<string, string[]>
    code?: string
    message?: string
}