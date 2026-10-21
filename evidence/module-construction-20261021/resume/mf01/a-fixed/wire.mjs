// Isolated MF01 candidate transport. This is not a public or production API.
import { Buffer } from 'node:buffer'

export function packValue(value, seen = new Set()) {
  if (value === undefined) return ['undefined']
  if (value === null) return ['null']
  if (typeof value === 'string' || typeof value === 'boolean') return [typeof value, value]
  if (typeof value === 'number') {
    if (!Number.isFinite(value)) throw new Error('WIRE_NON_FINITE')
    return ['number', Object.is(value, -0) ? '-0' : value]
  }
  if (typeof value !== 'object') throw new Error('WIRE_NON_DATA')
  if (seen.has(value)) throw new Error('WIRE_CYCLE')
  if (Object.getOwnPropertySymbols(value).length) throw new Error('WIRE_SYMBOL')
  const prototype = Object.getPrototypeOf(value)
  if (!Array.isArray(value) && prototype !== null) {
    const constructor = Object.getOwnPropertyDescriptor(prototype, 'constructor')
    if (Object.getPrototypeOf(prototype) !== null || !constructor || typeof constructor.value !== 'function' || constructor.value.name !== 'Object') throw new Error('WIRE_NON_PLAIN')
  }
  seen.add(value)
  try {
    const read = key => {
      const property = Object.getOwnPropertyDescriptor(value, key)
      if (!property || !Object.hasOwn(property, 'value')) throw new Error('WIRE_ACCESSOR')
      return packValue(property.value, seen)
    }
    if (Array.isArray(value)) {
      if (Object.getOwnPropertyNames(value).length !== value.length + 1) throw new Error('WIRE_ARRAY_PROPERTY')
      return ['array', Array.from({ length: value.length }, (_, index) => read(String(index)))]
    }
    return ['object', Object.getOwnPropertyNames(value).map(key => [key, read(key)])]
  } finally { seen.delete(value) }
}

export function unpackValue(tagged) {
  const [kind, value] = tagged
  if (kind === 'undefined') return undefined
  if (kind === 'null') return null
  if (kind === 'string' || kind === 'boolean') return value
  if (kind === 'number') return value === '-0' ? -0 : value
  if (kind === 'array') return value.map(unpackValue)
  if (kind !== 'object') throw new Error('WIRE_BAD_TAG')
  const result = {}
  for (const [key, entry] of value) Object.defineProperty(result, key, { value: unpackValue(entry), enumerable: true, writable: true, configurable: true })
  return result
}

export function frame(message) {
  const payload = Buffer.from(JSON.stringify(packValue(message)), 'utf8')
  const header = Buffer.alloc(8)
  header.writeBigUInt64BE(BigInt(payload.length))
  return Buffer.concat([header, payload])
}

export class FrameReader {
  parts = []
  size = 0
  wanted = null
  push(bytes) {
    this.parts.push(Buffer.from(bytes))
    this.size += bytes.length
    const messages = []
    while (this.size >= (this.wanted ?? 8)) {
      const all = Buffer.concat(this.parts, this.size)
      if (this.wanted === null) {
        const length = all.readBigUInt64BE()
        if (length > BigInt(Number.MAX_SAFE_INTEGER)) throw new Error('WIRE_LENGTH')
        this.wanted = Number(length)
        this.parts = [all.subarray(8)]
        this.size -= 8
      } else {
        const text = new TextDecoder('utf-8', { fatal: true }).decode(all.subarray(0, this.wanted))
        messages.push(unpackValue(JSON.parse(text)))
        this.parts = [all.subarray(this.wanted)]
        this.size -= this.wanted
        this.wanted = null
      }
    }
    return messages
  }
  finish() { if (this.size !== 0 || this.wanted !== null) throw new Error('WIRE_INCOMPLETE') }
}

export function errorData(error) {
  return { name: String(error?.name ?? 'Error'), message: String(error?.message ?? error), code: typeof error?.code === 'string' ? error.code : null }
}
