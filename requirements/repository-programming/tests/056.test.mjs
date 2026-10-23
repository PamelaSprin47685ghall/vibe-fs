import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { parse as parseToml } from 'smol-toml'
import {
  execute,
  caseName,
  parkedHandle,
  render,
} from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import {
  installDelegationHost,
  clearDelegationHost,
} from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-delegation-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
  }
}`

const runProgram = (dir, body, sessionId) =>
  execute(dir, 'Manager', 'en', sessionId, { name: 'delegation-envelope', program: program(body) }, 3000, Date.now() + 60_000, 1 << 20, null)

// A stateful fake host: every delegation member travels the single
// `(name, payload, carry)` entry; delegated charges are remembered
// until the next join consumes them.
const fakeHost = () => {
  const pending = []
  const calls = []
  let counter = 0
  return {
    pending,
    calls,
    delegate: async (name, payload, carry) => {
      calls.push({ member: name, payload, carry })
      if (name === 'devops') {
        return { ok: true, returned: [{ text: 'stub-devops' }] }
      }
      if (name !== 'fork' && name !== 'commission' && name !== 'resume') {
        return { ok: false, code: 'unknown-delegate' }
      }
      if (!payload || !payload.charge) return { ok: false, code: 'charge-required' }
      counter += 1
      const agentId = `agent-${counter}`
      pending.push({ name: payload.name, agentId, member: name })
      return { ok: true, name: payload.name, agentId, outcome: 'created' }
    },
    join: async () => {
      const records = pending.splice(0)
      return {
        ok: true,
        records: records.map((entry) => ({
          name: entry.name,
          agentId: entry.agentId,
          role: 'engineer',
          status: 'completed',
          returned: [{ text: `record:${entry.name}` }],
        })),
      }
    },
    fission: async (payload) => ({
      ok: true,
      groupId: 'group-1',
      lanes: Array.from({ length: payload.lanes }, (_, index) => ({ index, sessionId: `lane-${index}` })),
    }),
    horizon: async () => ({ ok: true, agents: [], ptys: [] }),
    advisor: async () => ({ ok: true, advice: 'stub-advice' }),
  }
}

const install = (sessionId, host) =>
  installDelegationHost(sessionId, host.delegate, host.join, host.fission, host.horizon, host.advisor)

test('WHAT[repository-programming-056] delegation members return data envelopes, never prose', async () => {
  const { dir, cleanup } = sandbox()
  const sessionId = `sess-env-${Math.random().toString(36).slice(2, 8)}`
  try {
    const host = fakeHost()
    install(sessionId, host)
    const outcome = await runProgram(
      dir,
      `    const placed = await this.fork("index the parser module", [1]);
    const joined = await this.join();
    return { placed, joined };`,
      sessionId,
    )
    assert.equal(caseName(outcome), 'Succeeded')
    const doc = render(outcome)
    assert.match(doc, /name = "index-the-parser-module"/, 'the name is derived from the charge')
    assert.match(doc, /outcome = "created"/)
    assert.match(doc, /record:index-the-parser-module/, 'join returns the delivered view')
    assert.deepEqual(parseToml(doc).data.joined.records[0].returned, [{ text: 'record:index-the-parser-module' }])
    assert.match(doc, /\[\[data\.joined\.records\.returned\]\]/, 'returned entries are actual TOML tables')

    // fission belongs to roles that hold the Fission permission (Engineer).
    install(`${sessionId}-fission`, host)
    const fissionOutcome = await execute(
      dir,
      'Engineer',
      'en',
      `${sessionId}-fission`,
      {
        name: 'fission-envelope',
        program: program(
          `    return await this.fission({ charge: "explore three designs", lanes: 2 });`,
        ),
      },
      3000,
      Date.now() + 60_000,
      1 << 20,
      null,
    )
    assert.equal(caseName(fissionOutcome), 'Succeeded')
    const fissionDoc = render(fissionOutcome)
    assert.match(fissionDoc, /groupId = "group-1"/)
    assert.match(fissionDoc, /sessionId = "lane-0"/)
  } finally {
    clearDelegationHost(sessionId)
    cleanup()
  }
})

test('WHAT[repository-programming-056] this.horizon serves the installed host roster and refuses with DELEGATION_UNAVAILABLE without one', async () => {
  const { dir, cleanup } = sandbox()
  const sessionId = `sess-horizon-${Math.random().toString(36).slice(2, 8)}`
  const roster = {
    ok: true,
    agents: [
      {
        label: 'Ada',
        status: 'still-away',
        workState: 'none',
        record: 'stub-horizon-roster',
        turnsUsed: 0,
        turnsLeft: 0,
        timeUsedMs: 0,
        timeLeftMs: 0,
      },
    ],
    ptys: [],
  }
  const notExercised = async () => {
    throw new Error('the horizon member must not reach fork/join/fission')
  }
  try {
    installDelegationHost(
      sessionId,
      notExercised,
      notExercised,
      notExercised,
      async () => roster,
      notExercised,
    )
    const wired = await runProgram(dir, `    return await this.horizon();`, sessionId)
    assert.equal(caseName(wired), 'Succeeded')
    const wiredDoc = render(wired)
    assert.match(wiredDoc, /status = "still-away"/, 'the host roster reaches the program as data')
    assert.match(wiredDoc, /record = "stub-horizon-roster"/, 'the member returns the host envelope untouched')
    assert.match(wiredDoc, /ptys = \[\]/)

    clearDelegationHost(sessionId)
    const unwired = await runProgram(dir, `    return await this.horizon();`, sessionId)
    assert.equal(caseName(unwired), 'Succeeded')
    assert.match(render(unwired), /code = "DELEGATION_UNAVAILABLE"/)
  } finally {
    clearDelegationHost(sessionId)
    cleanup()
  }
})

test('WHAT[repository-programming-056] fork and dag require the Fork capability', async () => {
  const { dir, cleanup } = sandbox()
  const sessionId = `sess-nofork-${Math.random().toString(36).slice(2, 8)}`
  try {
    const host = fakeHost()
    install(sessionId, host)
    const outcome = await execute(
      dir,
      'Engineer',
      'en',
      sessionId,
      { name: 'fork-capability', program: program(`    return { allowFork: typeof this.fork, allowDag: typeof this.dag };`) },
      3000,
      Date.now() + 60_000,
      1 << 20,
      null,
    )
    assert.equal(caseName(outcome), 'Succeeded')
    const doc = render(outcome)
    assert.match(doc, /allowFork = "undefined"/)
    assert.match(doc, /allowDag = "undefined"/)
  } finally {
    clearDelegationHost(sessionId)
    cleanup()
  }
})

test('WHAT[repository-programming-056] a refusal envelope reaches the program as data', async () => {
  const { dir, cleanup } = sandbox()
  const sessionId = `sess-refuse-${Math.random().toString(36).slice(2, 8)}`
  try {
    const host = fakeHost()
    install(sessionId, host)
    const outcome = await runProgram(
      dir,
      `    const refused = await this.fork({}, [1]);
    return { refused, continued: true };`,
      sessionId,
    )
    assert.equal(caseName(outcome), 'Succeeded')
    const doc = render(outcome)
    assert.match(doc, /code = "charge-required"/)
    assert.match(doc, /continued = true/, 'a refusal does not throw; the program keeps running')
  } finally {
    clearDelegationHost(sessionId)
    cleanup()
  }
})

test('WHAT[repository-programming-056] a missing host yields the DELEGATION_UNAVAILABLE envelope', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const outcome = await runProgram(
      dir,
      `    return await this.fork("nowhere", [1]);`,
      'sess-056-nohost',
    )
    assert.equal(caseName(outcome), 'Succeeded')
    const doc = render(outcome)
    assert.match(doc, /code = "DELEGATION_UNAVAILABLE"/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-056] fork starts a delegate with payload alone', async () => {
  const { dir, cleanup } = sandbox()
  const sessionId = `sess-no-carry-${Math.random().toString(36).slice(2, 8)}`
  try {
    const host = fakeHost()
    install(sessionId, host)
    const outcome = await runProgram(
      dir,
      `    const placed = await this.fork("index the parser module");
    const continued = "kept-running";
    return { placed, continued };`,
      sessionId,
    )
    assert.equal(caseName(outcome), 'Succeeded')
    const doc = render(outcome)
    assert.match(doc, /outcome = "created"/)
    assert.match(doc, /continued = "kept-running"/)
    assert.equal(host.calls.length, 1)
  } finally {
    clearDelegationHost(sessionId)
    cleanup()
  }
})


test('WHAT[repository-programming-056] dag runs dependencies in waves and carries their records', async () => {
  const { dir, cleanup } = sandbox()
  const sessionId = `sess-dag-${Math.random().toString(36).slice(2, 8)}`
  try {
    const host = fakeHost()
    install(sessionId, host)
    const outcome = await runProgram(
      dir,
      `    return await this.dag({ nodes: [
      { id: "spec", charge: "write the interface spec" },
      { id: "impl", charge: "implement it", deps: ["spec"] },
    ] });`,
      sessionId,
    )
    assert.equal(caseName(outcome), 'Succeeded')
    const doc = render(outcome)
    assert.match(doc, /record:spec/)
    assert.match(doc, /record:impl/)
    assert.match(doc, /skipped = \[\]/)
    assert.equal(host.calls[1].payload.charge, 'implement it', 'the task contains only its own intent')
    assert.doesNotMatch(doc, /byName|view =/, 'returned entries have one structured representation')
  } finally {
    clearDelegationHost(sessionId)
    cleanup()
  }
})

test('WHAT[repository-programming-056] a failed dag node parks and the injected skip decides', async () => {
  const { dir, cleanup } = sandbox()
  const sessionId = `sess-dagpark-${Math.random().toString(36).slice(2, 8)}`
  try {
    const host = fakeHost()
    // The "impl" node's child fails at terminal.
    const failingJoin = async () => {
      const records = host.pending.splice(0)
      return {
        ok: true,
        records: records.map((entry) => ({
          name: entry.name,
          agentId: entry.agentId,
          role: 'engineer',
          status: entry.name === 'impl' ? 'failed' : 'completed',
          returned: entry.name === 'impl' ? [] : [{ text: `record:${entry.name}` }],
        })),
      }
    }
    installDelegationHost(sessionId, host.delegate, failingJoin, host.fission, host.horizon, host.advisor)

    const parked = await runProgram(
      dir,
      `    return await this.dag({ nodes: [
      { id: "spec", charge: "write the interface spec" },
      { id: "impl", charge: "implement it", deps: ["spec"] },
    ] }, [1]);`,
      sessionId,
    )
    assert.equal(caseName(parked), 'Parked')
    const doc = render(parked)
    assert.match(doc, /code = "node-failed"/)
    assert.match(doc, /# parked/)

    const resumed = await execute(
      dir,
      'Manager',
      'en',
      sessionId,
      { name: 'skip-failed-dag-node', unpark: { handle: parkedHandle(parked), value: 'skip' } },
      3000,
      Date.now() + 60_000,
      1 << 20,
      null,
    )
    assert.equal(caseName(resumed), 'Succeeded')
    const resumedDoc = render(resumed)
    assert.match(resumedDoc, /skipped = \["impl"\]/)
  } finally {
    clearDelegationHost(sessionId)
    cleanup()
  }
})

test('WHAT[repository-programming-056] a voided child reports status voided in join and parks dag on node-voided', async () => {
  const { dir, cleanup } = sandbox()
  const sessionId = `sess-dagvoided-${Math.random().toString(36).slice(2, 8)}`
  let implAgentId = null
  try {
    const host = fakeHost()
    const voidedJoin = async () => {
      const records = host.pending.splice(0)
      const mapped = records.map((entry) => ({
        name: entry.name,
        agentId: entry.agentId,
        role: 'engineer',
        status: entry.name === 'impl' ? 'voided' : 'completed',
        returned: entry.name === 'impl' ? [] : [{ text: `record:${entry.name}` }],
      }))
      return {
        ok: true,
        records: mapped,
      }
    }
    // 已作废（voided）的子工作已结算：父方被告知一次，自行决定重试、跳过或替换。
    const trackingDelegate = async (name, payload, carry) => {
      const res = await host.delegate(name, payload, carry)
      if (res.ok && res.name === 'impl') {
        implAgentId = res.agentId
      }
      return res
    }
    installDelegationHost(sessionId, trackingDelegate, voidedJoin, host.fission, host.horizon, host.advisor)

    const joinOutcome = await runProgram(
      dir,
      `    await this.fork({ charge: "implement it", name: "impl" }, [1]);
    return await this.join();`,
      sessionId,
    )
    assert.equal(caseName(joinOutcome), 'Succeeded')
    const joinDoc = render(joinOutcome)
    assert.match(joinDoc, /status = "voided"/)
    assert.match(joinDoc, /returned = \[\]/)
    // voided 表示子工作已作废而非完成：义务已结清，父方被告知一次后决定重试/跳过/替换。
    assert.ok(implAgentId !== null, 'forked agent id must be tracked')

    const parked = await runProgram(
      dir,
      `    return await this.dag({ nodes: [
      { id: "spec", charge: "write the interface spec" },
      { id: "impl", charge: "implement it", deps: ["spec"] },
    ] }, [1]);`,
      sessionId,
    )
    assert.equal(caseName(parked), 'Parked')
    const doc = render(parked)
    assert.match(doc, /code = "node-voided"/)
    assert.match(doc, /# parked/)

    const resumed = await execute(
      dir,
      'Manager',
      'en',
      sessionId,
      { name: 'replace-voided-dag-node', unpark: { handle: parkedHandle(parked), value: { returned: [{ text: 'manual-impl' }] } } },
      3000,
      Date.now() + 60_000,
      1 << 20,
      null,
    )
    assert.equal(caseName(resumed), 'Succeeded')
    const resumedDoc = render(resumed)
    assert.match(resumedDoc, /manual-impl/)
  } finally {
    clearDelegationHost(sessionId)
    cleanup()
  }
})

