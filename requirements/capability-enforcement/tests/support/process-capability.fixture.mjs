import assert from 'node:assert/strict'
import { readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import * as quiescence from '../../../../dist/OpenCode/Host/QuiescenceSurface.js'
import {
  SESSION, accepted, rejected, assertNativeSerializationRejected,
  assertCodecSerializationRejected, createDurableMaterial, assertMaterialsCannotAdmit, assertLeaseNonDurability,
} from './quiescence-durability.mjs'

const directory = process.env.WXS_CAPABILITY_FIXTURE_DIRECTORY
const phase = process.env.WXS_CAPABILITY_FIXTURE_PHASE
assert.equal(typeof directory, 'string')
assert.ok(directory.length > 0)
assert.ok(phase === 'produce' || phase === 'recover', 'unknown fixture phase must fail, not register zero tests')
const materialPath = join(directory, 'durable-material.json')

if (phase === 'produce') {
  test('WHAT[capability-enforcement-019] producer rejects live permit and execution lease serialization while emitting only durable evidence', async () => {
    const gate = quiescence.create()
    quiescence.beginAttempt(gate, SESSION)
    const permit = quiescence.observeIdle(gate, SESSION)
    assertNativeSerializationRejected(permit)
    const material = createDurableMaterial()
    assertCodecSerializationRejected(permit, material)
    await assertLeaseNonDurability(material)
    assertMaterialsCannotAdmit(gate, material)
    assert.deepEqual(quiescence.tryConsume(gate, permit), accepted, 'refused serialization must not consume live permission')
    assert.deepEqual(quiescence.tryRelease(gate, permit), accepted)
    assert.deepEqual(quiescence.tryConsume(gate, permit), accepted, 'existing exact-permit return semantics must not change')
    quiescence.beginAttempt(gate, SESSION)
    const fresh = quiescence.observeIdle(gate, SESSION)
    assertNativeSerializationRejected(fresh)
    assert.deepEqual(quiescence.tryConsume(gate, fresh), accepted)
    writeFileSync(materialPath, JSON.stringify(material), { encoding: 'utf8', flag: 'wx' })
    writeFileSync(join(directory, 'producer.json'), JSON.stringify({ pid: process.pid, session: SESSION }), { flag: 'wx' })
  })
} else {
  test('WHAT[capability-enforcement-019] successor rejects restored evidence and historical idle and establishes its own fresh permit and execution lease', async () => {
    const materialBytes = readFileSync(materialPath, 'utf8')
    const material = JSON.parse(materialBytes)
    await assertLeaseNonDurability(material)
    const gate = quiescence.create()
    assertMaterialsCannotAdmit(gate, material)
    const historicalIdle = quiescence.observeIdle(gate, material.session)
    const historicalAdmission = quiescence.tryConsume(gate, historicalIdle)
    assert.deepEqual(historicalAdmission, rejected('NoFreshIdle'))
    assert.deepEqual(quiescence.tryRelease(gate, historicalIdle), rejected('NoFreshIdle'))
    assert.deepEqual(quiescence.tryConsume(gate, quiescence.observeIdle(gate, material.session)), rejected('NoFreshIdle'))

    quiescence.beginAttempt(gate, material.session)
    assert.deepEqual(quiescence.tryConsume(gate, historicalIdle), rejected('Superseded'))
    const fresh = quiescence.observeIdle(gate, material.session)
    assertNativeSerializationRejected(fresh)
    assertMaterialsCannotAdmit(gate, material)
    const freshAdmission = quiescence.tryConsume(gate, fresh)
    assert.deepEqual(freshAdmission, accepted, 'restored bytes and rejected callbacks must leave fresh admission intact')
    const duplicateAdmission = quiescence.tryConsume(gate, fresh)
    assert.deepEqual(duplicateAdmission, rejected('AlreadyConsumed'))
    assert.deepEqual(quiescence.tryRelease(gate, historicalIdle), rejected('Superseded'))
    assert.deepEqual(quiescence.tryConsume(gate, fresh), rejected('AlreadyConsumed'), 'old callback must not reopen fresh permission')
    assert.equal(readFileSync(materialPath, 'utf8'), materialBytes, 'recovery must not rewrite its durable evidence')
    writeFileSync(join(directory, 'successor.json'), JSON.stringify({
      pid: process.pid, session: material.session, historicalAdmission, freshAdmission, duplicateAdmission,
    }), { flag: 'wx' })
  })
}
