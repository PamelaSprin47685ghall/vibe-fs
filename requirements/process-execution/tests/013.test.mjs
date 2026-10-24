import assert from 'node:assert/strict'
import test from 'node:test'
import { sandbox, runProgram, caseName, parseData, nodeCommand } from './support/executor.mjs'
import * as toolResultBound from '../../../dist/Host/Contract/ToolResultBound.js'

// Host-native `run` tool is retired; `this.call` supersedes it (repository-programming [050]-[052]).

test('WHAT[process-execution-013] bounded raw tail preserves UTF-8 while complete output remains retrievable',async()=>{
  const {readFileSync}=await import('node:fs')
  const {dir,cleanup}=sandbox('wxs-proc013-tail-')
  try {
    const raw='HEAD-'+'中文🔥'.repeat(1000)+'-TAIL'
    const cmd=nodeCommand('process.stdout.write('+JSON.stringify(raw)+');process.stderr.write("warning")')
    const output=await runProgram(dir,'const r=await this.call('+JSON.stringify(cmd)+');return {exitCode:r.exitCode,stdoutTail:r.stdout.slice(-50),stdoutLength:r.stdout.length,validUtf8:!r.stdout.includes("\\uFFFD"),stderr:r.stderr,bytes:r.bytes,truncated:r.truncated,truncatedBytes:r.truncatedBytes,outputFiles:r.outputFiles};',undefined,undefined,2048)
    assert.equal(caseName(output),'Succeeded')
    const result=parseData(output)
    assert.equal(result.exitCode,0);assert.equal(result.truncated,true)
    assert.equal(result.validUtf8,true)
    assert.ok(raw.endsWith(result.stdoutTail))
    const actualTailBytes=Buffer.byteLength(raw.slice(-result.stdoutLength))
    assert.ok(actualTailBytes<=2048)
    assert.equal(result.bytes.stdout,Buffer.byteLength(raw))
    assert.equal(result.truncatedBytes.stdout,Buffer.byteLength(raw)-actualTailBytes)
    assert.equal(result.stderr,'warning')
    assert.equal(readFileSync(result.outputFiles.stdout,'utf8'),raw)
  } finally {cleanup()}
})

test('WHAT[process-execution-013] small raw outputs preserve separate streams without truncation',async()=>{
  const {dir,cleanup}=sandbox('wxs-proc013-small-')
  try {
    const output=await runProgram(dir,'return await this.call("printf hello; printf warning >&2");')
    assert.equal(caseName(output),'Succeeded')
    const result=parseData(output)
    assert.equal(result.truncated,false);assert.equal(result.stdout,'hello');assert.equal(result.stderr,'warning')
  } finally {cleanup()}
})

test('WHAT[process-execution-013] ToolResultBound keeps deterministic raw tail when output exceeds host window', () => {
  const original = 'EARLY-ERROR-\n' + 'x'.repeat(60000) + '\nLATEST-RAW-OUTPUT'
  const bounded = toolResultBound.bound(original)
  assert.ok(bounded.startsWith(toolResultBound.Marker))
  assert.ok(bounded.endsWith('\nLATEST-RAW-OUTPUT'))
  assert.equal(toolResultBound.fitsInWindow(bounded), true)
})
