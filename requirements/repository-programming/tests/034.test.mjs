import test from 'node:test'
import { fixture, assert, data, caseName, failureCode, parkedName, parkedPayload, parkedSource, render, created, readFileSync, writeFileSync, existsSync, pendingNames } from './support/named-program.fixture.mjs'

test('WHAT[repository-programming-034] abandon runs native finally and records real cleanup effects', async t => {
  const h=fixture(t,'034')
  writeFileSync(h.file('resource'),'open')
  await h.run('target',"try { await this.park({ready:true}); } finally { this.rm('resource');this.write('cleanup','done'); } return 'survived';")
  const result=data(await h.abandon('target'))
  assert.equal(result.state,'abandoned')
  assert.equal(existsSync(h.file('resource')),false)
  assert.equal(readFileSync(h.file('cleanup'),'utf8'),'done')
  assert.equal(result.effects.created.includes('cleanup'),true)
  assert.equal(result.effects.removed.includes('resource'),true)
})
test('WHAT[repository-programming-034] catching cancellation cannot promote return to success', async t => {
  const h=fixture(t,'034-catch')
  await h.run('target',"try { await this.park({ready:true}); } catch (_) {} return {swallowed:true};")
  const result=data(await h.abandon('target'))
  assert.equal(result.state,'abandoned');assert.notDeepEqual(result.value,{swallowed:true})
})
