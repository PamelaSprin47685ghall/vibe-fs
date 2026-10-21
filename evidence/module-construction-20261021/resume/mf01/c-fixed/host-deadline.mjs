// Isolated candidate policy. The exact GuestOwner owns this object and its timer.
export class HostDeadline {
  constructor(expire) {
    this.expire = expire
    this.current = null
    this.remaining = null
    this.leases = new Map()
    this.enabled = false
    this.closed = false
    this.cleanup = false
    this.timer = null
  }
  finite(value) {
    if (typeof value !== 'number' || !Number.isFinite(value)) throw new Error('DEADLINE_FINITE_REQUIRED')
    return value
  }
  schedule() {
    clearTimeout(this.timer)
    this.timer = null
    if (!this.enabled || this.closed || this.current === null) return
    const remaining = this.current - Date.now()
    this.timer = setTimeout(() => {
      this.timer = null
      if (!this.enabled || this.closed) return
      if (!this.admit()) return
      this.schedule()
    }, Math.max(0, Math.min(2147483647, remaining)))
  }
  update(now) {
    let latest = now + this.remaining
    for (const epoch of this.leases.values()) latest = Math.max(latest, epoch)
    this.current = latest
    this.schedule()
  }
  reset(epoch) {
    this.finite(epoch)
    if (this.closed) throw new Error('DEADLINE_OWNER_CLOSED')
    if (this.cleanup) return
    if (this.leases.size === 0) this.current = epoch
    else {
      const now = Date.now()
      this.remaining = Math.max(0, epoch - now)
      this.update(now)
    }
    this.schedule()
  }
  activate() {
    if (this.closed) throw new Error('DEADLINE_OWNER_CLOSED')
    this.enabled = true
    this.schedule()
  }
  suspend() {
    this.enabled = false
    clearTimeout(this.timer)
    this.timer = null
  }
  admit() {
    if (this.closed || !this.enabled || this.current === null) return false
    if (Date.now() < this.current) return true
    this.close()
    this.expire()
    return false
  }
  borrow(milliseconds) {
    this.finite(milliseconds)
    if (milliseconds <= 0) throw new Error('DEADLINE_POSITIVE_REQUIRED')
    if (!this.admit()) throw new Error('DEADLINE_EXPIRED')
    if (this.cleanup) return () => {}
    const now = Date.now()
    if (this.leases.size === 0) this.remaining = Math.max(0, this.current - now)
    const lease = Object.freeze({})
    this.leases.set(lease, this.finite(now + milliseconds))
    this.update(now)
    let released = false
    return () => {
      if (released) return
      released = true
      if (this.closed || this.cleanup || !this.leases.delete(lease)) return
      if (this.leases.size === 0) {
        this.current = Date.now() + this.remaining
        this.remaining = null
        this.schedule()
      } else this.update(Date.now())
    }
  }
  enterCleanup(epoch) {
    this.finite(epoch)
    if (this.closed) throw new Error('DEADLINE_OWNER_CLOSED')
    if (this.cleanup) return
    this.cleanup = true
    this.leases.clear()
    this.remaining = null
    this.current = epoch
    this.schedule()
  }
  remainingMs() { return this.current === null ? 0 : Math.max(0, this.current - Date.now()) }
  close() {
    this.closed = true
    this.enabled = false
    clearTimeout(this.timer)
    this.timer = null
  }
}
