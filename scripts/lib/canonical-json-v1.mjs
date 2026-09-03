import { createHash } from 'node:crypto'

export class CanonicalJsonV1Error extends TypeError {
  constructor(code, path, message) {
    super(`${path}: ${message}`)
    this.name = 'CanonicalJsonV1Error'
    this.code = code
    this.path = path
  }
}

const fail = (code, path, message) => {
  throw new CanonicalJsonV1Error(code, path, message)
}

const assertScalarString = (value, path) => {
  for (let index = 0; index < value.length; index += 1) {
    const unit = value.charCodeAt(index)
    if (unit >= 0xd800 && unit <= 0xdbff) {
      const next = value.charCodeAt(index + 1)
      if (!(next >= 0xdc00 && next <= 0xdfff)) {
        fail('canonical-json-unpaired-surrogate', path, 'high surrogate has no matching low surrogate')
      }
      index += 1
    } else if (unit >= 0xdc00 && unit <= 0xdfff) {
      fail('canonical-json-unpaired-surrogate', path, 'low surrogate has no matching high surrogate')
    }
  }
}

const codePoints = (value) => Array.from(value, (scalar) => scalar.codePointAt(0))

export const compareCanonicalTextV1 = (left, right) => {
  if (left === right) return 0
  const leftPoints = codePoints(left)
  const rightPoints = codePoints(right)
  const length = Math.min(leftPoints.length, rightPoints.length)
  for (let index = 0; index < length; index += 1) {
    if (leftPoints[index] !== rightPoints[index]) return leftPoints[index] < rightPoints[index] ? -1 : 1
  }
  return leftPoints.length < rightPoints.length ? -1 : 1
}

const encode = (value, path, ancestors) => {
  if (value === null) return 'null'
  if (typeof value === 'boolean') return value ? 'true' : 'false'
  if (typeof value === 'string') {
    assertScalarString(value, path)
    return JSON.stringify(value)
  }
  if (typeof value === 'number') {
    if (!Number.isSafeInteger(value) || value < 0 || Object.is(value, -0)) {
      fail('canonical-json-invalid-number', path, 'number must be a non-negative safe integer')
    }
    return String(value)
  }
  if (typeof value !== 'object') {
    fail('canonical-json-invalid-value', path, `unsupported ${typeof value}`)
  }
  if (ancestors.has(value)) fail('canonical-json-cycle', path, 'cyclic value is not canonical JSON')
  ancestors.add(value)
  try {
    if (Array.isArray(value)) {
      const ownKeys = Reflect.ownKeys(value)
      const hasNonIndexKey = ownKeys.some((key) => {
        if (key === 'length') return false
        return typeof key !== 'string' || !/^(0|[1-9][0-9]*)$/.test(key) || Number(key) >= value.length
      })
      if (hasNonIndexKey) {
        fail('canonical-json-invalid-value', path, 'array must not contain non-index properties')
      }
      for (let index = 0; index < value.length; index += 1) {
        if (!Object.hasOwn(value, index)) fail('canonical-json-sparse-array', `${path}[${index}]`, 'array must be dense')
        const descriptor = Object.getOwnPropertyDescriptor(value, String(index))
        if (!descriptor.enumerable || !Object.hasOwn(descriptor, 'value')) {
          fail('canonical-json-invalid-value', `${path}[${index}]`, 'array elements must be enumerable data values')
        }
      }
      return `[${value.map((item, index) => encode(item, `${path}[${index}]`, ancestors)).join(',')}]`
    }
    const prototype = Object.getPrototypeOf(value)
    if (prototype !== Object.prototype && prototype !== null) {
      fail('canonical-json-non-plain-object', path, 'object must have Object.prototype or null prototype')
    }
    const ownKeys = Reflect.ownKeys(value)
    if (ownKeys.some((key) => typeof key !== 'string')) fail('canonical-json-invalid-value', path, 'symbol keys are not canonical JSON')
    const keys = ownKeys
    for (const key of keys) {
      const descriptor = Object.getOwnPropertyDescriptor(value, key)
      if (!descriptor.enumerable || !Object.hasOwn(descriptor, 'value')) {
        fail('canonical-json-invalid-value', `${path}.${key}`, 'properties must be enumerable data values')
      }
    }
    for (const key of keys) assertScalarString(key, `${path}.<key>`)
    keys.sort(compareCanonicalTextV1)
    return `{${keys.map((key) => `${JSON.stringify(key)}:${encode(value[key], `${path}.${key}`, ancestors)}`).join(',')}}`
  } finally {
    ancestors.delete(value)
  }
}

export const encodeCanonicalJsonV1 = (value) => encode(value, '$', new WeakSet())

export const sha256BytesV1 = (bytes) => `sha256:${createHash('sha256').update(bytes).digest('hex')}`

export const canonicalDigestV1 = (domain, value) => {
  if (typeof domain !== 'string' || !domain.endsWith('\0')) {
    throw new TypeError('canonical digest domain must be a NUL-terminated string')
  }
  const hash = createHash('sha256')
  hash.update(Buffer.from(domain, 'utf8'))
  hash.update(Buffer.from(encodeCanonicalJsonV1(value), 'utf8'))
  return `sha256:${hash.digest('hex')}`
}

export const assertRepositoryPathV1 = (value, path = '$') => {
  if (typeof value !== 'string' || value.length === 0) fail('canonical-repository-path', path, 'path must be a non-empty string')
  assertScalarString(value, path)
  const segments = value.split('/')
  if (value.startsWith('/') || value.includes('\\') || segments.some((segment) => segment.length === 0 || segment === '.' || segment === '..') || /^[A-Za-z]:/.test(value)) {
    fail('canonical-repository-path', path, 'path must be canonical repository-relative POSIX text')
  }
  return value
}
