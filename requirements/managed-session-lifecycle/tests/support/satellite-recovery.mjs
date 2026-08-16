import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { agentJournal, errorResult, okResult, toList } from '../../../verification-system/tests/support/domain.mjs'

const satelliteModule = await import('../../../../dist/Execution/Session/Attachment/SatelliteRuntime.js')
const createRuntime = Object.entries(satelliteModule).find(([k]) => k.startsWith('SatelliteRuntime_$ctor'))?.[1]
const ensure = Object.entries(satelliteModule).find(([k]) => k.startsWith('SatelliteRuntime__Ensure_'))?.[1]
const { SatelliteSpec } = satelliteModule
const { SatelliteKind } = await import('../../../../dist/Execution/Session/Association.js')
const { SessionIdModule_create: sessionId } = await import('../../../../dist/Foundation/Identity.js')
const { AgentJournalCompanionPort } = await import('../../../../dist/Context/Companion/JournalPort.js')
const { OpenCodeChildInfo } = await import('../../../../dist/OpenCode/Host/OpenCodePort.js')
const {
  CompanionHost,
  CompanionHost__EnsureBloggerAsync,
  CompanionHost__InvalidateBloggerCache,
} = await import('../../../../dist/Context/Companion/Host.js')

const COMPANION_AGENT = 'fast-blogger'
const unionName = (value) => value.cases()[value.tag]
const resultName = unionName
const payload = (value) => value.fields[0]
const sessionValue = (value) => value.fields[0]

const emptyHost = (created) => ({
  ListChildren: async () => okResult(toList([])),
  CreateChildSession: async () => {
    const id = sessionId(`created-${created.length + 1}`)
    created.push(sessionValue(id))
    return okResult(id)
  },
  AbortSession: async () => okResult(undefined),
  FamilyRootOf: () => sessionId('root'),
})

export const directCompanionRepointFatal = async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-satellite-cut-fatal-'))
  const opened = await agentJournal.create({ directory: dir })
  if (!opened.ok) throw new Error(JSON.stringify(opened.error))
  const recorded = []
  const originalError = console.error
  console.error = (value) => recorded.push(String(value))
  try {
    const durable = new AgentJournalCompanionPort(opened.journal)
    const owner = sessionId('work-cut-fatal')
    const first = await durable.LinkBlogger(owner, sessionId('blogger-old-cut'), COMPANION_AGENT)
    if (resultName(first) !== 'Ok') throw new Error(String(payload(first)))
    const repoint = await durable.LinkBlogger(owner, sessionId('blogger-illegal-new'), COMPANION_AGENT)
    return { result: resultName(repoint), recorded }
  } finally {
    console.error = originalError
    opened.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
}

export const durableCompanionReplacement = async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-satellite-replace-'))
  const opened = await agentJournal.create({ directory: dir })
  if (!opened.ok) throw new Error(JSON.stringify(opened.error))
  try {
    const durable = new AgentJournalCompanionPort(opened.journal)
    const owner = sessionId('work-real')
    const old = sessionId('blogger-old-real')
    const seeded = await durable.LinkBlogger(owner, old, COMPANION_AGENT)
    if (resultName(seeded) !== 'Ok') throw new Error(String(payload(seeded)))

    const created = []
    const runtime = createRuntime(emptyHost(created))
    const replacementSpec = new SatelliteSpec(
      SatelliteKind.Companion,
      COMPANION_AGENT,
      COMPANION_AGENT,
      '/workspace',
      old,
      (main, blogger, agent) => durable.LinkBlogger(main, blogger, agent),
      (main) => durable.CloseBlogger(main),
    )

    const result = await ensure(runtime, owner, replacementSpec)
    if (resultName(result) !== 'Ok') return { ok: false, error: String(payload(result)), created }
    const lease = payload(result)
    const loaded = await durable.Load(owner)
    if (resultName(loaded) !== 'Ok') return { ok: false, error: String(payload(loaded)), created }
    const memory = payload(loaded)
    return {
      ok: true,
      origin: unionName(lease.Origin),
      created,
      bloggerId: sessionValue(memory.BloggerSessionId),
    }
  } finally {
    opened.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
}

const withFatalSuppressed = async (body) => {
  const previous = process.env.WANXIANGSHU_NO_FATAL_EXIT
  process.env.WANXIANGSHU_NO_FATAL_EXIT = '1'
  try {
    return await body()
  } finally {
    if (previous === undefined) delete process.env.WANXIANGSHU_NO_FATAL_EXIT
    else process.env.WANXIANGSHU_NO_FATAL_EXIT = previous
  }
}

export const companionCacheInvalidationRereadsDurableAssociation = async ({ oldSurvives }) =>
  withFatalSuppressed(async () => {
    const dir = mkdtempSync(join(tmpdir(), 'wxs-satellite-cache-durable-'))
    const opened = await agentJournal.create({ directory: dir })
    if (!opened.ok) throw new Error(JSON.stringify(opened.error))
    try {
      const durable = new AgentJournalCompanionPort(opened.journal)
      const owner = sessionId(`work-cache-${oldSurvives ? 'reuse' : 'replace'}`)
      const old = sessionId(`blogger-old-cache-${oldSurvives ? 'reuse' : 'replace'}`)
      const seeded = await durable.LinkBlogger(owner, old, COMPANION_AGENT)
      if (resultName(seeded) !== 'Ok') throw new Error(String(payload(seeded)))

      let listOld = true
      let createCalls = 0
      const sessions = {
        FamilyRootOf: (sid) => sid,
        ListChildren: async () => okResult(toList(listOld ? [
          new OpenCodeChildInfo(old, owner, COMPANION_AGENT, COMPANION_AGENT),
        ] : [])),
        CreateChildSession: async () => {
          createCalls += 1
          return okResult(sessionId(`blogger-replacement-${createCalls}`))
        },
        AbortSession: async () => okResult(undefined),
      }
      const runtime = createRuntime(sessions)
      const host = new CompanionHost(
        owner,
        sessions,
        durable,
        undefined,
        sessionValue(old),
        opened.journal,
        '/workspace',
        runtime,
      )

      const first = await CompanionHost__EnsureBloggerAsync(host)
      CompanionHost__InvalidateBloggerCache(host)
      listOld = oldSurvives

      let second
      let secondError = ''
      try {
        second = await CompanionHost__EnsureBloggerAsync(host)
      } catch (error) {
        secondError = error?.message ?? String(error)
      }
      const loaded = await durable.Load(owner)
      const current = resultName(loaded) === 'Ok' ? payload(loaded).BloggerSessionId : undefined
      return {
        first: sessionValue(first),
        second: second === undefined ? undefined : sessionValue(second),
        secondError,
        createCalls,
        durableBlogger: current === undefined ? undefined : sessionValue(current),
      }
    } finally {
      opened.dispose()
      rmSync(dir, { recursive: true, force: true })
    }
  })

export const failedCompanionEnsureRetry = async () => {
  let listCalls = 0
  let createCalls = 0
  const sessions = {
    FamilyRootOf: (sid) => sid,
    ListChildren: async () => {
      listCalls += 1
      if (listCalls === 1) return errorResult('temporary host snapshot failure')
      return okResult(toList([]))
    },
    CreateChildSession: async () => {
      createCalls += 1
      return okResult(sessionId(`retry-created-${createCalls}`))
    },
    AbortSession: async () => okResult(undefined),
  }
  const runtime = createRuntime(sessions)
  const host = new CompanionHost(
    sessionId('work-retry'),
    sessions,
    undefined,
    undefined,
    undefined,
    undefined,
    '/workspace',
    runtime,
  )

  let firstError = ''
  try {
    await CompanionHost__EnsureBloggerAsync(host)
  } catch (error) {
    firstError = error?.message ?? String(error)
  }
  const recovered = await CompanionHost__EnsureBloggerAsync(host)
  return { firstError, recoveredId: sessionValue(recovered), listCalls, createCalls }
}
