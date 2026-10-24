import test from 'node:test'
import { fixture, assert, data, caseName, failureCode, parkedName, parkedPayload, parkedSource, render, created, readFileSync, writeFileSync, existsSync, pendingNames } from './support/named-program.fixture.mjs'

test('WHAT[repository-programming-048] source preserves every line and only marks a reliable model location', async t => {
  const h=fixture(t,'048')
  const result=await h.run('explicit',"const v=await this.park({stage:'review'});\nreturn v;")
  assert.equal(caseName(result),'Parked')
  const lines=parkedSource(result).split('\n')
  assert.ok(lines.every(line=>line.startsWith(' | ')||line.startsWith('>| ')))
  assert.match(lines.map(line=>line.slice(3)).join('\n'),/class Js extends JsProgram/)
  for(const line of lines.filter(line=>line.startsWith('>| ')))assert.match(line,/this\.park/)
  assert.match(render(result),/source = (?:'''|""")/)
})
test('WHAT[repository-programming-048] ambiguous where keeps model source without marking the generated wrapper', async t => {
  const h=fixture(t,'048-where')
  const result=await h.run('where',"return await this.where('x x','x');")
  assert.equal(caseName(result),'Parked')
  for(const line of parkedSource(result).split('\n').filter(line=>line.startsWith('>| '))){
    assert.match(line,/this\.where/);assert.doesNotMatch(line,/_api/)
  }
})
test('WHAT[repository-programming-048] evaluated code cannot create a false source location or an execution escape', async t => {
  const h=fixture(t,'048-eval')
  const result=await h.run('eval',"return await eval('(async()=>{await this.park({});})()');")
  assert.equal(caseName(result),'Failed')
  assert.match(render(result),/Code generation from strings disallowed|EvalError/)
})
