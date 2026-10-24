import test from 'node:test'
import { fixture, assert, data, caseName, parkedPayload, render } from './support/named-program.fixture.mjs'
import { createApi, api as apiOf } from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'

test('WHAT[repository-programming-046] whereAll is literal and preserves original UTF-16 offsets', t => {
  const h=fixture(t,'046-literal'),api=apiOf(createApi(h.root))
  const text='alpha   beta\r\n gamma'
  assert.deepEqual(api.whereAll(text,'alpha beta gamma').spans,[])
  assert.deepEqual(api.whereAll(text,text).spans,[[0,text.length]])
  const astral='🙂 x 🙂 x'
  assert.deepEqual(api.whereAll(astral,'x').spans,[[3,4],[8,9]])
})
test('WHAT[repository-programming-046] whereAll returns non-overlapping matches and honors from', t => {
  const h=fixture(t,'046-all'),api=apiOf(createApi(h.root)),text='x middle x tail x'
  assert.equal(api.whereAll(text,'x').spans.length,3)
  const later=api.whereAll(text,'x',2).spans
  assert.equal(later.length,2);assert.equal(later[0][0],'x middle '.length)
})
test('WHAT[repository-programming-046] a unique where resolves without parking', async t => {
  const h=fixture(t,'046-unique')
  const result=await h.run('unique',"const text='alpha beta gamma';const [a,b]=await this.where(text,'beta');return {hit:text.slice(a,b)};")
  assert.equal(caseName(result),'Succeeded');assert.equal(data(result).hit,'beta')
})
test('WHAT[repository-programming-046] missing where parks with original needle and candidates', async t => {
  const h=fixture(t,'046-missing')
  const result=await h.run('missing',"return await this.where('alpha beta gamma','alpah');")
  assert.equal(caseName(result),'Parked')
  const payload=parkedPayload(result).payload
  assert.equal(payload.needle,'alpah');assert.deepEqual(payload.candidates,[])
  assert.equal(payload.nearest.text,'alpha beta gamma')
})
test('WHAT[repository-programming-046] ambiguous where resumes the original expression with the chosen span', async t => {
  const h=fixture(t,'046-ambiguous')
  const result=await h.run('ambiguous',"const text='a x b x c';const [begin,end]=await this.where(text,'x');return {hit:text.slice(begin,end),begin,end};")
  assert.equal(caseName(result),'Parked')
  const candidates=parkedPayload(result).payload.candidates
  assert.deepEqual(candidates.map(c=>c.span[0]),[2,6])
  const receipt=data(await h.unpark('ambiguous',candidates[1].span))
  assert.equal(receipt.state,'completed');assert.deepEqual(receipt.value,{hit:'x',begin:6,end:7})
})
test('WHAT[repository-programming-046] a wrong span reports target failure inside the control receipt', async t => {
  const h=fixture(t,'046-bad-value')
  await h.run('ambiguous',"const [a,b]=await this.where('x x','x');return [a,b];")
  const result=await h.unpark('ambiguous',{not:'a span'})
  assert.equal(caseName(result),'Succeeded')
  assert.equal(data(result).state,'failed')
})
