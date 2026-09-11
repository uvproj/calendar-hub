import type {
    AggregateEventsResponse,
    AntiforgeryToken,
    BatchCreationResponse,
    CalendarDraft,
    CalendarInterpretationResponse,
    CalendarSummary,
    CurrentUser,
    Member,
    ProblemDetails,
} from './types'

type MutationMethod = 'POST' | 'PUT'

export class ApiError extends Error {
    readonly status: number
    readonly validationErrors: Record<string, string[]>
    readonly code?: string

    constructor(
        message: string,
        status: number,
        validationErrors: Record<string, string[]> = {},
        code?: string,
    ) {
        super(message)
        this.name = 'ApiError'
        this.status = status
        this.validationErrors = validationErrors
        this.code = code
    }
}

export class ApiClient {
    private antiforgeryToken?: AntiforgeryToken

    getCurrentUser(signal?: AbortSignal) {
        return this.request<CurrentUser>('/api/auth/me', { signal })
    }

    login(credentials: { username: string; passcode: string }, signal?: AbortSignal) {
        return this.mutate<CurrentUser>('/api/auth/login', 'POST', credentials, signal)
    }

    bootstrap(request: { username: string; displayName: string; passcode: string; bootstrapSecret: string }, signal?: AbortSignal) {
        return this.mutate<CurrentUser>('/api/auth/bootstrap', 'POST', request, signal)
    }

    async logout(signal?: AbortSignal) {
        await this.mutate<void>('/api/auth/logout', 'POST', undefined, signal)
    }

    getCalendars(signal?: AbortSignal) {
        return this.request<CalendarSummary[]>('/api/calendars', { signal })
    }

    getEvents(start: string, end: string, calendars: string[], signal?: AbortSignal) {
        const query = new URLSearchParams({ start, end })
        calendars.forEach((calendar) => query.append('calendar', calendar))
        return this.request<AggregateEventsResponse>(`/api/events?${query}`, { signal })
    }

    interpret(text: string, destinationCalendar: string, signal?: AbortSignal) {
        return this.mutate<CalendarInterpretationResponse>(
            '/api/calendar-interpretations',
            'POST',
            { text, destinationCalendar },
            signal,
        )
    }

    createBatch(destinationCalendar: string, events: CalendarDraft[], signal?: AbortSignal) {
        return this.mutate<BatchCreationResponse>('/api/events/batch', 'POST', { destinationCalendar, events }, signal)
    }

    getMembers(signal?: AbortSignal) {
        return this.request<Member[]>('/api/admin/members/', { signal })
    }

    createMember(request: { username: string; displayName: string; passcode: string; isAdministrator: boolean }, signal?: AbortSignal) {
        return this.mutate<Member>('/api/admin/members', 'POST', request, signal)
    }

    setMemberActive(memberId: string, isActive: boolean, signal?: AbortSignal) {
        return this.mutate<Member>(`/api/admin/members/${encodeURIComponent(memberId)}/active`, 'PUT', { isActive }, signal)
    }

    async resetPasscode(memberId: string, passcode: string, signal?: AbortSignal) {
        await this.mutate<void>(`/api/admin/members/${encodeURIComponent(memberId)}/passcode`, 'PUT', { passcode }, signal)
    }

    private async mutate<T>(url: string, method: MutationMethod, body?: unknown, signal?: AbortSignal): Promise<T> {
        const token = await this.getAntiforgeryToken(signal)
        return this.request<T>(url, {
            method,
            signal,
            headers: {
                'Content-Type': 'application/json',
                [token.headerName]: token.requestToken,
            },
            body: body === undefined ? undefined : JSON.stringify(body),
        })
    }

    private async getAntiforgeryToken(signal?: AbortSignal): Promise<AntiforgeryToken> {
        if (!this.antiforgeryToken) {
            this.antiforgeryToken = await this.request<AntiforgeryToken>('/api/auth/antiforgery', { signal })
        }
        return this.antiforgeryToken
    }

    private async request<T>(url: string, init: RequestInit): Promise<T> {
        const response = await fetch(url, { ...init, credentials: 'include' })
        if (!response.ok) {
            throw await this.toApiError(response)
        }
        if (response.status === 204) {
            return undefined as T
        }
        return await response.json() as T
    }

    private async toApiError(response: Response): Promise<ApiError> {
        let problem: ProblemDetails = {}
        try {
            problem = await response.json() as ProblemDetails
        } catch {
            problem = {}
        }
        return new ApiError(
            problem.title ?? problem.message ?? problem.detail ?? `Request failed with status ${response.status}.`,
            response.status,
            problem.errors ?? {},
            problem.code,
        )
    }
}

export const apiClient = new ApiClient()