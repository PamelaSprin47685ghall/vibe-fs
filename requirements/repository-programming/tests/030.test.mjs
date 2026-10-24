import test from 'node:test'
import { fixture, assert, data, caseName, failureCode, parkedName, parkedPayload, parkedSource, render, created, readFileSync, writeFileSync, existsSync, pendingNames } from './support/named-program.fixture.mjs'

test('WHAT[repository-programming-030] explicit park stops later effects until named resume', async t => {
  const h=fixture(t,'030')
  const result=await h.run('review',"const value=await this.park({stage:'review'});this.write('decision',JSON.stringify(value));return value;")
  assert.equal(caseName(result),'Parked');assert.equal(parkedName(result),'review')
  assert.deepEqual(parkedPayload(result).payload,{stage:'review'})
  assert.equal(existsSync(h.file('decision')),false)
  const resumed=await h.unpark('review',{accepted:true})
  assert.equal(caseName(resumed),'Succeeded')
  assert.equal(data(resumed).name,'review');assert.equal(data(resumed).state,'completed')
  assert.equal(readFileSync(h.file('decision'),'utf8'),'{"accepted":true}')
})
test('WHAT[repository-programming-030] control is scoped to its session and cannot consume a foreign pause', async t => {
  const a=fixture(t,'030-owner'),b=fixture(t,'030-foreign')
  await a.run('private',"await this.park({private:true});this.write('continued','yes');return 1;")
  const foreign=await b.unpark('private')
  assert.equal(caseName(foreign),'Succeeded')
  assert.equal(data(foreign).state,'failed')
  assert.equal(data(foreign).code,'PROGRAM_UNKNOWN')
  assert.equal(existsSync(a.file('continued')),false)
  assert.deepEqual(Array.from(pendingNames(a.session)),['private'])
  assert.equal(data(await a.unpark('private')).state,'completed')
})
