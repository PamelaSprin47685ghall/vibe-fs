import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as concern from '../../../dist/Interaction/Concern/Surface.js'

const read = (path) => readFileSync(path, 'utf8')

test('WHAT[concern-routing-007] routing state remains mailbox facts plus bounded delivery coverage, not an organization workflow', () => {
  const source = read('src/Wanxiangshu/Interaction/Concern/Projection.fs')
  assert.doesNotMatch(source, /\b(Priority|Deadline|OrganizationGraph|PresenceAuthority|WorkflowEngine|AckProtocol)\b/)
})
