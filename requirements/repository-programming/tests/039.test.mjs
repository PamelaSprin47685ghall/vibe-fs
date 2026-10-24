import test from 'node:test'
import { fixture, assert, data, caseName, failureCode, parkedName, parkedPayload, parkedSource, render, created, readFileSync, writeFileSync, existsSync, pendingNames } from './support/named-program.fixture.mjs'

import { existsSync as exists, readFileSync as read } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { closeSession } from '../../../dist/Repository/Programming/Js/Programs.js'
test('WHAT[repository-programming-039] host composition delegates all program state to the public library', () => {
  const sourcePath=new URL('../../../src/Wanxiangshu/Repository/Programming/Js/Programs.fs',import.meta.url)
  const text=read(fileURLToPath(sourcePath),'utf8')
  assert.match(text,/Import\("Program", "wanxiangli\/program"\)/)
  assert.doesNotMatch(text,/Dictionary|concurrencyLimit|setTimeout|type .*Run/)
  for(const path of ['Repository/Programming/Js/ParkedSites.fs','Process/JsSandbox.fs','Process/JsCallLoop.fs']){
    assert.equal(exists(fileURLToPath(new URL('../../../src/Wanxiangshu/'+path,import.meta.url))),false,path)
  }
})
test('WHAT[repository-programming-039] closing one session confirms cleanup without removing another session', async t => {
  const a=fixture(t,'039-a'),b=fixture(t,'039-b')
  await a.run('same',"try{await this.park({ready:true});}finally{this.write('closed','yes');}return 1;")
  await b.run('same',"await this.park({ready:true});return 2;")
  await closeSession(a.session)
  assert.equal(readFileSync(a.file('closed'),'utf8'),'yes')
  assert.deepEqual(Array.from(pendingNames(a.session)),[])
  assert.deepEqual(Array.from(pendingNames(b.session)),['same'])
})
