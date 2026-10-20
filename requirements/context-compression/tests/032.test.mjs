import assert from 'node:assert/strict'
import test from 'node:test'
import {
  applyView,
  captureView,
  clearAll,
  parseReportMapping,
  reportMaterial,
  takeView,
} from '../../../dist/Repository/Programming/Js/Anchor/AnchorSurface.js'
import { JsFailureModule_code } from '../../../dist/Repository/Programming/Js/Failure.js'

const session = 'report-material'
const input = (...texts) => texts.map((text, index) => ({ id: String(index + 1), text }))

// The compiled surface hands back an F# `Result`; the payload of a successful
// report is an F# list of `(anchor, text)` tuples. Both are normalized here so
// the assertions stay on observable values only.
const isOk = (result) => result?.tag === 0 || result?.ok === true

const payload = (result) => {
  if (result?.tag === 0) return result.fields[0]
  return result?.value ?? result?.material ?? result?.entries ?? null
}

const failureCode = (result) => {
  if (!result) return null
  if (result.tag === 1) return JsFailureModule_code(result.fields[0])
  if (typeof result.code === 'string') return result.code
  if (result instanceof Error) return result.code ?? result.message
  return null
}

const pair = (entry) => (Array.isArray(entry) ? [entry[0], entry[1]] : [entry.anchor, entry.text])

const material = (result) => {
  const value = payload(result)
  if (value === null || value === undefined) return []
  if (Array.isArray(value)) return value.map(pair)
  const out = []
  let cell = value
  while (cell && cell.tail != null) {
    out.push(pair(cell.head))
    cell = cell.tail
  }
  return out
}

const texts = (result) => material(result).map(([, text]) => text)

test.beforeEach(() => clearAll())

test('WHAT[context-compression-032] report material is a read-only projection: it repeats and leaves the pending view pending', () => {
  const messages = input('one', 'two', 'three')
  applyView(session, messages)
  assert.equal(captureView(session, { prefixUntil: 1, thenMap: { 1: null } }).ok, true)

  const first = reportMaterial(session, { 2: 'summary-two' })
  const second = reportMaterial(session, { 2: 'summary-two' })

  assert.equal(isOk(first), true, 'a legal report mapping must succeed')
  assert.deepEqual(material(first), material(second), 'the same mapping projects the same material')
  assert.deepEqual(
    material(first).find(([, text]) => text === 'summary-two'),
    [2, 'summary-two'],
  )

  assert.deepEqual(
    takeView(session),
    { prefixUntil: 1, thenMap: { 1: null } },
    'the pending view is still pending and unchanged after the projection',
  )
  assert.equal(takeView(session), null, 'only the take consumes the pending view')
})

test('WHAT[context-compression-032] report material writes no permanent compression into the session ledger', () => {
  const messages = input('one', 'two', 'three')
  applyView(session, messages)

  const projected = reportMaterial(session, { 2: 'summary-two' })
  assert.equal(isOk(projected), true)
  assert.deepEqual(
    material(projected).find(([, text]) => text === 'summary-two'),
    [2, 'summary-two'],
  )

  const after = applyView(session, messages)
  assert.deepEqual(after.report.replaced, [], 'the projection recorded no replacement')
  assert.deepEqual(
    after.entries.map((entry) => (entry.kind === 'kept' ? entry.message.text : entry.summary)),
    ['one', 'two', 'three'],
    'the original content survives a projection that showed a summary',
  )
})

test('WHAT[context-compression-032] an illegal report mapping is refused with VIEW_DECLARATION_INVALID', () => {
  const illegal = [
    { 1: 7 },
    { 1: '' },
    { 1: '   ' },
    { 1: true },
    { 1: {} },
    { '-1': 'summary' },
    { '1.5': 'summary' },
    { x: 'summary' },
  ]

  for (const mapping of illegal) {
    const result = reportMaterial(session, mapping)
    assert.equal(isOk(result), false, JSON.stringify(mapping))
    assert.equal(failureCode(result), 'VIEW_DECLARATION_INVALID', JSON.stringify(mapping))
  }

  assert.equal(isOk(parseReportMapping({ 1: 'summary' })), true)
  assert.equal(isOk(parseReportMapping({ 1: 7 })), false)
  assert.equal(failureCode(parseReportMapping({ 1: 7 })), 'VIEW_DECLARATION_INVALID')
})

test('WHAT[context-compression-032] an empty report mapping is legal and introduces no summary material', () => {
  const bare = reportMaterial('report-material-empty', {})
  assert.equal(isOk(bare), true, 'an empty mapping is a legal mapping')
  assert.deepEqual(material(bare), [], 'an empty mapping projects no material')

  const messages = input('one', 'two')
  applyView(session, messages)
  const projected = reportMaterial(session, {})
  assert.equal(isOk(projected), true)
  for (const text of texts(projected)) {
    assert.ok(['one', 'two'].includes(text), `unexpected material ${text}`)
  }
})
