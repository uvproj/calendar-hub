export function isoToInput(value: string, allDay: boolean): string {
    if (allDay) return value.slice(0, 10)
    const date = new Date(value)
    const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000)
    return local.toISOString().slice(0, 16)
}

export function inputToOffsetIso(value: string, allDay: boolean): string {
    const localValue = allDay ? `${value}T00:00:00` : `${value}:00`
    const localDate = new Date(localValue)
    const offsetMinutes = -localDate.getTimezoneOffset()
    const sign = offsetMinutes >= 0 ? '+' : '-'
    const absolute = Math.abs(offsetMinutes)
    const hours = String(Math.floor(absolute / 60)).padStart(2, '0')
    const minutes = String(absolute % 60).padStart(2, '0')
    return `${localValue}${sign}${hours}:${minutes}`
}