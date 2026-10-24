import test from 'node:test'
import { fixture, assert, data, caseName, failureCode, parkedName, parkedPayload, parkedSource, render, created, readFileSync, writeFileSync, existsSync, pendingNames } from './support/named-program.fixture.mjs'

test('WHAT[repository-programming-028] return ends only its named program and leaves other obligations pending', async t => {
  const h=fixture(t,'028')
  const paused=await h.run('waiting',"await this.park({stage:'review'}); return 'done';")
  assert.equal(caseName(paused),'Parked')
  const completed=await h.run('answer',"return {answer:42};")
  assert.equal(caseName(completed),'Succeeded')
  assert.deepEqual(data(completed),{answer:42})
  assert.deepEqual(Array.from(pendingNames(h.session)),['waiting'])
})
test('WHAT[repository-programming-028] a failed program preserves physical effects without reporting delivery', async t => {
  const h=fixture(t,'028-failure')
  const result=await h.run('failed',"this.write('evidence','landed'); throw new Error('expected');")
  assert.equal(caseName(result),'Failed')
  assert.equal(failureCode(result),'PROGRAM_FAILED')
  assert.equal(readFileSync(h.file('evidence'),'utf8'),'landed')
  assert.deepEqual(Array.from(created(result)),['evidence'])
})
