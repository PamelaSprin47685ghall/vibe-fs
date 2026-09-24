import assert from 'node:assert/strict'
import test from 'node:test'
import * as prefix from '../../../dist/Context/Prefix/Surface.js'

test('WHAT[context-compression-020] only_the_opening_and_the_last_K_phases_are_retained', () => {
  // The caller's own verdict is carried across the boundary unchanged. The retired
  // todowrite exemption kept every ledger round raw forever; nothing may reintroduce
  // a per-tool-name retention rule here.
  assert.deepEqual(
    prefix.retainedPrefixMessages([
      { retained: false },
      { retained: true },
      { retained: true },
      { retained: false },
    ]),
    [false, true, true, false],
    'the caller decides retention; the surface never invents one',
  )
})

test('WHAT[context-compression-020] a_retained_cognitive_round_needs_no_tool_name_to_survive', () => {
  // The K-window rule keeps a round because it is inside the window, not because of
  // which tool produced it. A round that names no recognisable tool is retained
  // exactly as the caller decided.
  assert.deepEqual(
    prefix.retainedPrefixMessages([
      { retained: true, tool: 'assume' },
      { retained: true, tool: 'read' },
      { retained: true },
    ]),
    [true, true, true],
    'retention follows the phase window, never the tool name',
  )
})
