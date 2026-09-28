// A structural check for WHAT[js-semantic-surface-005]. It cannot identify
// compiler semantics disguised as plain data. Validate Promise results and
// function results separately; opaque handles use assertOpaque.

const isPlainObject = (value) => {
  if (typeof value !== 'object' || value === null) return false
  const proto = Object.getPrototypeOf(value)
  return proto === Object.prototype || proto === null
}

export const isJsData = (value, seen = new Set()) => {
  if (value === null || value === undefined) return true
  const type = typeof value
  if (type === 'string' || type === 'number' || type === 'boolean' || type === 'bigint' || type === 'function') {
    return true
  }
  if (type !== 'object') return false
  if (value instanceof Promise) return true
  if (seen.has(value)) return true // shared/cyclic structure: still JS-native
  seen.add(value)
  if (Array.isArray(value)) return value.every((item) => isJsData(item, seen))
  if (isPlainObject(value)) {
    return Object.values(value).every((item) => isJsData(item, seen))
  }
  // FSharpMap / FSharpSet / record runtime class / any class instance.
  return false
}

export const assertJsData = (value, label = 'value') => {
  if (!isJsData(value)) {
    throw new Error(`${label} is not JS-native data (Fable representation leaked across a semantic surface)`)
  }
  return value
}

/** An opaque resource handle is a capability token: tests may obtain it, pass
 *  it back, and dispose it — never inspect it. Accepts any object/function,
 *  rejects primitives that cannot carry identity. */
export const assertOpaque = (value, label = 'handle') => {
  if (value === null || value === undefined || (typeof value !== 'object' && typeof value !== 'function')) {
    throw new Error(`${label} is not an opaque handle`)
  }
  return value
}
