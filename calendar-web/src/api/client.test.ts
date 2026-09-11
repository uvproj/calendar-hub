import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiClient } from './client'

describe('ApiClient', () => {
    beforeEach(() => {
        vi.restoreAllMocks()
    })

    it('includes credentials and a cached antiforgery header on every mutation', async () => {
        const fetchMock = vi.spyOn(globalThis, 'fetch')
            .mockResolvedValueOnce(new Response(JSON.stringify({ requestToken: 'csrf-token', headerName: 'X-CSRF-TOKEN' }), {
                status: 200,
                headers: { 'Content-Type': 'application/json' },
            }))
            .mockResolvedValueOnce(new Response(JSON.stringify({ id: '1', username: 'alex', displayName: 'Alex', isAdministrator: false }), {
                status: 200,
                headers: { 'Content-Type': 'application/json' },
            }))
            .mockResolvedValueOnce(new Response(null, { status: 204 }))

        const client = new ApiClient()
        await client.login({ username: 'alex', passcode: 'family-passcode' })
        await client.logout()

        expect(fetchMock).toHaveBeenNthCalledWith(1, '/api/auth/antiforgery', expect.objectContaining({ credentials: 'include' }))
        expect(fetchMock).toHaveBeenNthCalledWith(2, '/api/auth/login', expect.objectContaining({
            credentials: 'include',
            headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf-token' }),
        }))
        expect(fetchMock).toHaveBeenNthCalledWith(3, '/api/auth/logout', expect.objectContaining({
            headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf-token' }),
        }))
    })

    it('normalizes ProblemDetails and supports cancellation', async () => {
        vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(new Response(JSON.stringify({
            title: 'The request is invalid.',
            status: 400,
            errors: { Name: ['Name is required.'] },
        }), { status: 400, headers: { 'Content-Type': 'application/problem+json' } }))

        const signal = new AbortController().signal
        await expect(new ApiClient().getCalendars(signal)).rejects.toEqual(expect.objectContaining({
            status: 400,
            message: 'The request is invalid.',
            validationErrors: { Name: ['Name is required.'] },
        }))
        expect(fetch).toHaveBeenCalledWith('/api/calendars', expect.objectContaining({ signal }))
    })
})