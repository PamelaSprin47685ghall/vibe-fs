// Process owner API: PTY signal, command, identity and read contracts.

import assert from 'node:assert/strict'
import test from 'node:test'

const {
  signalParse,
  ptySignalView,
  ptyCommandSpawn,
  ptyCommandWrite,
  ptyCommandRead,
  ptyCommandSignal,
  ptyCommandResize,
  ptyCommandView,
  ptyId,
  ptyIdView,
  createPtyPort,
  portFork,
  portComplete,
  portRead,
  portReadResult,
  portList,
} = await import('../../../dist/Process/Surface.js')

test('WHAT[PROC-001] PTY_TYPES_tryParse_accepts_every_supported_signal_name', () => {
  const expected = [
    ['TERM', 'SIGTERM'],
    ['KILL', 'SIGKILL'],
    ['INT', 'SIGINT'],
    ['HUP', 'SIGHUP'],
    ['QUIT', 'SIGQUIT'],
    ['USR1', 'SIGUSR1'],
    ['USR2', 'SIGUSR2'],
  ]
  for (const [wire, name] of expected) {
    assert.deepEqual(signalParse(wire), { ok: true, value: name }, wire)
    assert.equal(ptySignalView(wire), name)
  }
})

test('WHAT[PROC-001] PTY_TYPES_tryParse_rejects_unknown_and_prefixed_names', () => {
  for (const bad of ['SIGTERM', 'term', '', 'SIGKILL', 'STOP']) {
    const parsed = signalParse(bad)
    assert.equal(parsed.ok, false, bad)
    assert.match(String(parsed.error), /Unsupported PTY signal/)
    if (bad !== '') assert.ok(String(parsed.error).includes(bad), `${bad} echoed in error`)
  }
})

test('WHAT[PROC-001] PTY_TYPES_command_views_carry_their_fields', () => {
  assert.deepEqual(ptyCommandView(ptyCommandSpawn('sh -c ls', '/tmp')), {
    kind: 'Spawn',
    command: 'sh -c ls',
    cwd: '/tmp',
  })
  assert.deepEqual(ptyCommandView(ptyCommandWrite(new TextEncoder().encode('abc'))), {
    kind: 'Write',
    bytes: [97, 98, 99],
  })
  assert.deepEqual(ptyCommandView(ptyCommandRead()), { kind: 'Read' })
  assert.deepEqual(ptyCommandView(ptyCommandSignal('HUP')), { kind: 'Signal', signal: 'SIGHUP' })
  assert.deepEqual(ptyCommandView(ptyCommandResize(120, 40)), {
    kind: 'Resize',
    width: 120,
    height: 40,
  })
})

test('WHAT[PROC-001] PTY_TYPES_pty_id_roundtrips_its_value', () => {
  assert.equal(ptyIdView(ptyId('pty-deadbeef')), 'pty-deadbeef')
})

test('WHAT[PROC-001] PTY_TYPES_pty_handle_view_exposes_identity_and_command', () => {
  const port = createPtyPort({})
  const id = portFork(port, 'sleep 1', 'distiller', ptyId('pty-1'), undefined)
  const listed = portList(port).ptys
  assert.equal(listed.length, 1)
  assert.equal(listed[0].id, 'pty-1')
  assert.equal(listed[0].command, 'sleep 1')
  assert.equal(listed[0].agent, 'distiller')
  assert.ok(typeof listed[0].startedAt === 'string')
  assert.equal(ptyIdView(id), 'pty-1')
})

test('WHAT[PROC-001] PTY_TYPES_pty_read_view_reports_output_and_closed', async () => {
  const port = createPtyPort({})
  const id = portFork(port, 'echo hi', 'distiller', ptyId('pty-read'), undefined)
  const pending = portRead(port, id)
  portReadResult(port, id, 'partial output', true)
  assert.deepEqual(await pending, { ok: true, value: { output: 'partial output', closed: true } })
  portComplete(port, id, undefined)
})

test('WHAT[PROC-001] PTY_TYPES_read_plans_cover_unknown_in_progress_closed_and_park', async () => {
  const port = createPtyPort({})
  const unknown = await portRead(port, ptyId('pty-unknown'))
  assert.deepEqual(unknown, { ok: false, error: 'Unknown PTY id: pty-unknown' })

  const id = portFork(port, 'echo hi', 'distiller', ptyId('pty-plan'), undefined)
  const parked = portRead(port, id)
  assert.deepEqual(await portRead(port, id), { ok: false, error: 'PTY read already in progress' })
  portReadResult(port, id, '', false)
  assert.deepEqual(await parked, { ok: true, value: { output: '', closed: false } })

  portComplete(port, id, undefined)
  assert.deepEqual(await portRead(port, id), { ok: true, value: { output: '', closed: true } })
})
