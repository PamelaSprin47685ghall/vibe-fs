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

export const compareCanonicalTextV1 = (left, right) => {
  if (left === right) return 0
  let leftIndex = 0
  let rightIndex = 0
  while (leftIndex < left.length && rightIndex < right.length) {
    const leftPoint = left.codePointAt(leftIndex)
    const rightPoint = right.codePointAt(rightIndex)
    if (leftPoint !== rightPoint) return leftPoint < rightPoint ? -1 : 1
    leftIndex += leftPoint > 0xffff ? 2 : 1
    rightIndex += rightPoint > 0xffff ? 2 : 1
  }
  return leftIndex < left.length ? 1 : -1
}

const writeCanonicalJsonV1 = (value, path, ancestors, write) => {
  if (value === null) {
    write('null')
    return
  }
  if (typeof value === 'boolean') {
    write(value ? 'true' : 'false')
    return
  }
  if (typeof value === 'string') {
    assertScalarString(value, path)
    write(JSON.stringify(value))
    return
  }
  if (typeof value === 'number') {
    if (!Number.isSafeInteger(value) || value < 0 || Object.is(value, -0)) {
      fail('canonical-json-invalid-number', path, 'number must be a non-negative safe integer')
    }
    write(String(value))
    return
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
      write('[')
      for (let index = 0; index < value.length; index += 1) {
        if (index > 0) write(',')
        writeCanonicalJsonV1(value[index], `${path}[${index}]`, ancestors, write)
      }
      write(']')
      return
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
    write('{')
    for (let index = 0; index < keys.length; index += 1) {
      if (index > 0) write(',')
      const key = keys[index]
      write(`${JSON.stringify(key)}:`)
      writeCanonicalJsonV1(value[key], `${path}.${key}`, ancestors, write)
    }
    write('}')
  } finally {
    ancestors.delete(value)
  }
}

export const encodeCanonicalJsonV1 = (value) => {
  const chunks = []
  writeCanonicalJsonV1(value, '$', new WeakSet(), (chunk) => chunks.push(chunk))
  return chunks.join('')
}

export const sha256BytesV1 = (bytes) => `sha256:${createHash('sha256').update(bytes).digest('hex')}`

export const canonicalDigestV1 = (domain, value) => {
  if (typeof domain !== 'string' || !domain.endsWith('\0')) {
    throw new TypeError('canonical digest domain must be a NUL-terminated string')
  }
  const hash = createHash('sha256')
  hash.update(Buffer.from(domain, 'utf8'))
  writeCanonicalJsonV1(value, '$', new WeakSet(), (chunk) => hash.update(chunk, 'utf8'))
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
