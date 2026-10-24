import test from 'node:test'
import { fixture, assert, data, caseName, failureCode, parkedName, parkedPayload, parkedSource, render, created, readFileSync, writeFileSync, existsSync, pendingNames } from './support/named-program.fixture.mjs'

test('WHAT[repository-programming-032] unpark passes the value directly to the original expression', async t => {
  const h=fixture(t,'032')
  await h.run('target',"const value=await this.park({stage:'review'});this.write('decision',JSON.stringify(value));return value;")
  const result=await h.unpark('target',{go:'ahead'})
  assert.equal(caseName(result),'Succeeded')
  assert.deepEqual(data(result).value,{go:'ahead'})
  assert.equal(readFileSync(h.file('decision'),'utf8'),'{"go":"ahead"}')
  assert.deepEqual(Array.from(created(result)),[],'target effects stay in its receipt')
  assert.equal(data(result).effects.created.includes('decision'),true)
})
test('WHAT[repository-programming-032] another park retains the target name and requires another explicit resume', async t => {
  const h=fixture(t,'032-again')
  await h.run('target',"const first=await this.park({n:1});const second=await this.park({n:2});return {first,second};")
  const next=data(await h.unpark('target','one'))
  assert.equal(next.state,'parked');assert.equal(next.name,'target');assert.deepEqual(next.payload,{n:2})
  const final=data(await h.unpark('target','two'))
  assert.equal(final.state,'completed');assert.deepEqual(final.value,{first:'one',second:'two'})
})
test('WHAT[repository-programming-032] unknown names are explicit failures', async t => {
  const h=fixture(t,'032-unknown')
  const result=await h.unpark('missing')
  assert.equal(caseName(result),'Succeeded');assert.equal(data(result).state,'failed');assert.equal(data(result).code,'PROGRAM_UNKNOWN')
})
