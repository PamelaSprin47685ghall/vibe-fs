import test from 'node:test'
import { fixture, assert, data, caseName, failureCode } from './support/named-program.fixture.mjs'
test('WHAT[repository-programming-058] named signal and send report missing targets without new command effects', async t => {
  const h=fixture(t,'058','DevOps')
  for(const [member,value]of [['send','input'],['signal','SIGTERM']]){
    const result=await h.run(member,'return await this.'+member+'("missing",'+JSON.stringify(value)+');')
    assert.equal(caseName(result),'Succeeded')
    assert.equal(data(result).state,'failed')
    assert.equal(data(result).code,'PROGRAM_UNKNOWN')
  }
})
test('WHAT[repository-programming-058] legacy one-argument controls cannot select an implicit command', async t => {
  const h=fixture(t,'058-arity','DevOps')
  const result=await h.run('old-control','return await this.signal("SIGTERM");')
  assert.equal(caseName(result),'Failed')
  assert.equal(failureCode(result),'INVALID_ARGUMENT')
})
