// JS-SEMANTIC-SURFACE-005/006 representation validator (P5).
//
// The ONLY representation authority for semantic surfaces: Fable runtime values
// must not cross a new surface. Ordinary data must be JSON-shaped; opaque
// resource handles are capability tokens, not semantic data.
//
//   assertJsData(value)   — throw unless value is JS-native (JSON-shaped) data
//   assertOpaque(value)   — throw unless value can be used as an opaque handle
//   isJsData(value)       — boolean form, for gates and filters
//
// This module is deliberately Fable-free: it detects Fable shapes structurally
// (cases/tag+fields/head+tail/class instances/reflection metadata/Date) so a
// Fable runtime value can never masquerade as plain data.

const isPlainObject = (value) => {
  if (typeof value !== 'object' || value === null) return false
  const proto = Object.getPrototypeOf(value)
  return proto === Object.prototype || proto === null
}

/** JSON-shaped data, recursively. Dates, F# DUs, FSharpList/Map/Set, record
 *  runtime classes and reflection metadata are all rejected: the time boundary
 *  is ISO-8601 string / epoch milliseconds, and tag/fields/cases() are
 *  compiler vocabulary, not semantic contract. */
export const isJsData = (value, seen = new Set()) => {
  if (value === null || value === undefined) return true
  const type = typeof value
  if (type === 'string' || type === 'number' || type === 'boolean' || type === 'bigint' || type === 'function') {
    return true
  }
  if (type !== 'object') return false
  // Bare Date is the documented silent-timezone-bug boundary (facade meta
  // tests proved Date ↔ DateTimeOffset confusion). Time crosses as string/ms.
  if (value instanceof Date) return false
  // F# DU instance: `cases()` on the constructor or `tag` + `fields` on the
  // instance. A plain object with a `tag`/`fields` field is also forbidden —
  // those names are reserved against compiler vocabulary (charter forbidden
  // patterns).
  if (typeof value.cases === 'function') return false
  if (value.tag !== undefined && Array.isArray(value.fields)) return false
  // FSharpList: head + tail getters.
  if (value.head !== undefined && value.tail !== undefined) return false
  if (seen.has(value)) return true // shared/cyclic structure: still JS-native
  seen.add(value)
  if (Array.isArray(value)) return value.every((item) => isJsData(item, seen))
  if (isPlainObject(value)) {
    if ('$reflection' in value) return false // Fable reflection metadata
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
