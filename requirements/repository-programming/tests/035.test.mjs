import test from 'node:test'
import { fixture, assert, data, caseName, failureCode, parkedName, parkedPayload, parkedSource, render, created, readFileSync, writeFileSync, existsSync, pendingNames } from './support/named-program.fixture.mjs'

test('WHAT[repository-programming-035] cleanup cannot park again and authorized cleanup still runs', async t => {
  const h=fixture(t,'035')
  await h.run('target',"try {await this.park({stage:'first'});} finally {try {await this.park({stage:'again'});} catch(e) {this.write('refused',e.code);}} return 1;")
  const result=data(await h.abandon('target'))
  assert.equal(result.state,'abandoned')
  assert.equal(readFileSync(h.file('refused'),'utf8'),'CANNOT_PARK_WHILE_ABANDONING')
  assert.deepEqual(Array.from(pendingNames(h.session)),[])
})
