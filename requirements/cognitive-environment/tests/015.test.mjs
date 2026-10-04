import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { mkdirSync, mkdtempSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import * as BloggerChronicleSurface from '../../../dist/OpenCode/Host/BloggerChronicleSurface.js'
import * as ModelRoutingSurface from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'
import * as JournalSurface from '../../../dist/Persistence/Journal/Surface.js'
import * as LanguageSurface from '../../../dist/Participant/Provider/LanguageSurface.js'
import { acceptAuthorityRoot, claimBloggerRequest, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')

const transformsSource = readFileSync(join(root, 'src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs'), 'utf8')

const bloggerSource = readFileSync(join(root, 'src/Wanxiangshu/OpenCode/Host/BloggerChronicleText.fs'), 'utf8')

const chronicleResourcePath = 'resources/provider/cognitive-environment/blogger-chronicle-text'

const zhResource = readFileSync(join(root, chronicleResourcePath, 'zh-CN.md'), 'utf8').trim()
const enResource = readFileSync(join(root, chronicleResourcePath, 'en.md'), 'utf8').trim()

test('WHAT[cognitive-environment-015] BLOGGER_CHRONICLE_TEXT_has_exact_bilingual_craft', () => {
  assert.match(zhResource, /直接把材料提炼成 charge、occurrence、settlement、consequence，并调用 chronicle/)
  assert.match(
    enResource,
    /Distill the material into charge, occurrence, settlement, and consequence, then call chronicle directly\./,
  )
  // 源码不再内联文案，只引用资源语义路径（provider-language-006 成对叶子）
  assert.match(bloggerSource, /cognitive-environment\/blogger-chronicle-text/)
  assert.doesNotMatch(bloggerSource, /直接把材料提炼成/)
  assert.doesNotMatch(bloggerSource, /Distill the material into/)
})

test('WHAT[cognitive-environment-015] Blogger prose discipline rejects status-report wrappers and demands self-contained evidence', () => {
  const zh = readFileSync(join(root, 'resources/provider/role/blogger/zh-CN.md'), 'utf8')
  const en = readFileSync(join(root, 'resources/provider/role/blogger/en.md'), 'utf8')

  assert.match(zh, /经过分析/)
  assert.match(zh, /最短证据链/)
  assert.match(zh, /自包含/)
  assert.match(zh, /技术判词|判词/)
  assert.match(zh, /可证伪/)
  assert.match(en, /after reviewing/)
  assert.match(en, /shortest evidence chain/)
  assert.match(en, /self-contained/)
  assert.match(en, /verdict backed by evidence/)
  assert.match(en, /falsifiable/)
})

test('WHAT[cognitive-environment-015] BLOGGER_CHRONICLE_TEXT_is_companion_only_ephemeral_assistant_text_injection', () => {
  assert.match(bloggerSource, /SessionAssociationProjection\.isCompanion/)
  assert.match(bloggerSource, /"type", box "text"/)
  assert.match(bloggerSource, /"text", box text/)

  assert.doesNotMatch(bloggerSource, /AgentJournal\.append|appendDurable|GuidelineProjection|tryInject/)
  assert.doesNotMatch(bloggerSource, /PairProgrammingThoughtTransform|systemBlock|"reasoning"|"tool"|"status"|"source"|"synthetic"/)
})

test('WHAT[cognitive-environment-015] BLOGGER_CHRONICLE_TEXT_is_enabled_for_step_3_5_flash_model_prefix', () => {
  const enabledHelper = bloggerSource.match(/let private bloggerChronicleTextEnabled[\s\S]*?\n    let private rawMessageRole/)?.[0]
  assert.ok(enabledHelper, 'Blogger chronicle text model gate must remain a named local decision')
  assert.match(
    bloggerSource,
    /let private bloggerChronicleTextModelPrefixes\s*:\s*string list\s*=\s*\[\s*"step-3\.5-flash"\s*\]/,
  )
  assert.match(enabledHelper, /ModelRouting\.tryReadExecution/)
  assert.match(enabledHelper, /model\.modelID\.StartsWith\(prefix, StringComparison\.Ordinal\)/)
  assert.match(enabledHelper, /List\.exists[\s\S]*bloggerChronicleTextModelPrefixes/)
  assert.doesNotMatch(bloggerSource, /providerID[^\n]*step-3\.5-flash|Contains\([^\n]*step-3\.5-flash/)
})

test('WHAT[cognitive-environment-015] BLOGGER_CHRONICLE_TEXT_is_the_last_semantic_injection_before_sanitize', () => {
  const score = transformsSource.slice(
    transformsSource.indexOf('let normalTransform'),
    transformsSource.indexOf('let private ordinaryProviderTransform'),
  )
  const pairIndex = score.indexOf('caps.InjectPairGuideline')
  const groundingIndex = score.indexOf('caps.ProjectRequirementGrounding')
  const bloggerIndex = score.indexOf('caps.InjectBloggerChronicle')
  const sanitizeIndex = score.indexOf('caps.SanitizeMessages')

  assert.ok(pairIndex >= 0, 'Pair guideline transform must be present')
  assert.ok(bloggerIndex > pairIndex, 'Blogger chronicle text must be injected after pair guideline')
  assert.ok(bloggerIndex > groundingIndex, 'Blogger chronicle text must be injected after requirement grounding')
  assert.ok(sanitizeIndex > bloggerIndex, 'Message sanitize must occur after chronicle text')
})

// ---------------------------------------------------------------------------
// 行为级证据（GAP-077）：经正式登记的 BloggerChronicleSurface 走真实生产入口
// BloggerChronicleText.maybeInject。模型门禁读的是 process-shared ModelRouting
// 的 exact lease，因此测试用隔离 HOME 下的 wanxiangshu.mjs 控制 scheduler 路由
// 结果（与 host-boundary/tests/032 同一手法），journal 是真实 EventStore。
// ---------------------------------------------------------------------------

const schedulerHome = mkdtempSync(join(tmpdir(), 'wanxiangshu-blogger-routing-'))
mkdirSync(join(schedulerHome, '.config', 'opencode'), { recursive: true })
writeFileSync(
  join(schedulerHome, '.config', 'opencode', 'wanxiangshu.mjs'),
  `export const routingProtocol = 2
export const hasTheoreticalCapacity = () => true
export const predictorConfiguration = () => ({ state: 'unconfigured', reason: null })
export default function route() {
  return { model: globalThis.__wanxiangshu_test_blogger_model ?? 'test/step-3.5-flash-canary', reasoning: 'low' }
}
`,
)
const previousHome = process.env.HOME
process.env.HOME = schedulerHome
await ModelRoutingSurface.initialize()
if (previousHome === undefined) {
  delete process.env.HOME
} else {
  process.env.HOME = previousHome
}

const journalDirectory = mkdtempSync(join(tmpdir(), 'wanxiangshu-blogger-journal-'))
const journalBoot = await BloggerChronicleSurface.createJournal(journalDirectory)
assert.ok(journalBoot.ok, 'journal boot must succeed before behavioral cases')

const companionLink = await BloggerChronicleSurface.appendCompanionLink(journalBoot.journal, {
  session: 'ses-blog-main-1',
  bloggerSession: 'ses-blog-blogger-1',
  bloggerAgent: 'blogger',
})
assert.ok(companionLink.ok, 'companion link append must succeed')
assert.equal(companionLink.companion, true, 'linked blogger session must project as companion')

test.after(() => {
  BloggerChronicleSurface.disposeJournal(journalBoot.journal)
})

const acquireLease = async (session, physicalUserMessageId, role = 'blogger', participant = 'chronicler') => {
  const acquisition = await ModelRoutingSurface.acquireSharedExecutionAdmission(
    session,
    physicalUserMessageId,
    role,
    participant,
    null,
    'normal',
  )
  assert.equal(acquisition.kind, 'Acquired', 'shared admission must acquire an exact lease')
  // tryReadExecution observes only committed executions (emr-011
  // accept → acquire → project); the injection model gate reads the
  // committed lease target, so the fixture commits the admission the way
  // production does before the transform runs.
  const committed = ModelRoutingSurface.commitSharedExecutionAdmission(acquisition.lease, {
    sessionId: session,
    physicalUserMessageId,
    role,
    participant: 'chronicler',
    target: ModelRoutingSurface.sharedExecutionAdmissionTarget(acquisition.lease),
  })
  assert.equal(committed.kind, 'Applied', 'shared admission must commit the exact lease')
  return acquisition
}

const frontierUserMessage = (id) => ({
  info: { id, role: 'user' },
  parts: [{ type: 'text', text: '材料已备好，请记账' }],
})

const chronicleMarkerCount = (messages) =>
  messages.filter(
    (m) => m?.info?.role === 'assistant' && m?.parts?.length === 1 && m.parts[0]?.type === 'text' &&
      (m.parts[0]?.text === zhResource || m.parts[0]?.text === enResource),
  ).length

const snapshotJournalTree = (directory) => {
  const entries = {}
  const walk = (dir, prefix) => {
    for (const name of readdirSync(dir)) {
      const full = join(dir, name)
      const relative = prefix ? `${prefix}/${name}` : name
      if (statSync(full).isDirectory()) {
        walk(full, relative)
      } else {
        entries[relative] = createHash('sha256').update(readFileSync(full)).digest('hex')
      }
    }
  }
  walk(directory, '')
  return entries
}

test('WHAT[cognitive-environment-015] B1_whitelisted_model_prefix_injects_exactly_one_bilingual_hint_before_frontier_user', async () => {
  delete globalThis.__wanxiangshu_test_blogger_model
  const session = 'ses-blog-blogger-1'
  const physical = 'msg-user-b1'
  await acquireLease(session, physical)

  const frontier = frontierUserMessage(physical)
  const outObj = { messages: [frontier] }
  BloggerChronicleSurface.maybeInject(journalBoot.journal, session, 'zh-CN', outObj)

  assert.equal(outObj.messages.length, 2, 'whitelisted lease must inject exactly one message')
  const [marker, user] = outObj.messages
  assert.equal(marker.info.role, 'assistant')
  assert.ok(marker.info.id.startsWith('text-'), 'injected marker id must be the digest-derived text- id')
  assert.equal(marker.parts.length, 1)
  assert.equal(marker.parts[0].type, 'text')
  assert.equal(marker.parts[0].text, zhResource, 'injected text must be the zh-CN provider resource')
  assert.deepEqual(user, frontier, 'frontier user message must survive verbatim after the marker')
})

test('WHAT[cognitive-environment-015] B2_non_whitelisted_model_prefix_performs_zero_injection', async () => {
  globalThis.__wanxiangshu_test_blogger_model = 'test/other-model'
  try {
    const session = 'ses-blog-blogger-1'
    const physical = 'msg-user-b2'
    await acquireLease(session, physical)

    const frontier = frontierUserMessage(physical)
    const outObj = { messages: [frontier] }
    BloggerChronicleSurface.maybeInject(journalBoot.journal, session, 'zh-CN', outObj)

    assert.equal(outObj.messages.length, 1, 'non-whitelisted lease must inject nothing')
    assert.deepEqual(outObj.messages[0], frontier, 'original messages must remain untouched')
  } finally {
    delete globalThis.__wanxiangshu_test_blogger_model
  }
})

test('WHAT[cognitive-environment-015] B3_existing_same_text_assistant_message_is_deduplicated_not_reinjected', async () => {
  delete globalThis.__wanxiangshu_test_blogger_model
  const session = 'ses-blog-blogger-1'
  const physical = 'msg-user-b3'
  await acquireLease(session, physical)

  const frontier = frontierUserMessage(physical)
  const outObj = { messages: [frontier] }
  BloggerChronicleSurface.maybeInject(journalBoot.journal, session, 'zh-CN', outObj)
  assert.equal(chronicleMarkerCount(outObj.messages), 1, 'first injection adds exactly one marker')

  // 同一 outObj 再次经过 transform：既有同文本 assistant 消息被 filter 剔除后
  // 重新插入恰好一条，不产生重复。
  BloggerChronicleSurface.maybeInject(journalBoot.journal, session, 'zh-CN', outObj)
  assert.equal(outObj.messages.length, 2, 'replay must not accumulate duplicate markers')
  assert.equal(chronicleMarkerCount(outObj.messages), 1, 'exactly one chronicle marker survives the replay')
  assert.equal(outObj.messages[1].info.id, physical, 'marker stays before the frontier user message')
})

test('WHAT[cognitive-environment-015] B4_injection_writes_no_durable_persistence', async () => {
  delete globalThis.__wanxiangshu_test_blogger_model
  const session = 'ses-blog-blogger-1'
  const physical = 'msg-user-b4'
  await acquireLease(session, physical)

  const outObj = { messages: [frontierUserMessage(physical)] }
  const before = snapshotJournalTree(journalDirectory)
  BloggerChronicleSurface.maybeInject(journalBoot.journal, session, 'zh-CN', outObj)
  const after = snapshotJournalTree(journalDirectory)

  assert.equal(outObj.messages.length, 2, 'injection itself must still happen')
  assert.deepEqual(after, before, 'journal bytes must be untouched by the injection (no AgentJournal.append)')
})

test('WHAT[cognitive-environment-015] B5_language_binding_selects_the_matching_resource_leaf', async () => {
  delete globalThis.__wanxiangshu_test_blogger_model
  const session = 'ses-blog-blogger-1'
  const physical = 'msg-user-b5'
  await acquireLease(session, physical)

  const outObj = { messages: [frontierUserMessage(physical)] }
  BloggerChronicleSurface.maybeInject(journalBoot.journal, session, 'en', outObj)

  assert.equal(outObj.messages.length, 2)
  const marker = outObj.messages[0]
  assert.equal(marker.info.role, 'assistant')
  assert.equal(marker.parts[0].text, enResource, 'english binding must inject the en resource leaf verbatim')
  assert.notEqual(marker.parts[0].text, zhResource, 'english binding must not fall back to the zh-CN leaf')
})

// ---------------------------------------------------------------------------
// GAP-077 补充（registered Host 链）：经真实插件注册的
// experimental.chat.messages.transform 走完整 normalTransform（16 步真实
// capabilities），而非直调 BloggerChronicleSurface.maybeInject。lease 经
// ModelRouting 正式 surface 建立（与 B1—B5 同一 emr-011 accept→acquire→
// project owner 顺序），companion link 经 journal surface 写入
// CompanionBloggerLinked。ModelRouting 是 process singleton，插件二次启动的
// initialize 幂等，路由结果继续由本文件顶部的隔离 HOME 配置驱动
// （globalThis.__wanxiangshu_test_blogger_model）。
//
// 第九层（调用条件调查）结论：normalTransform 第 15 步 InjectBloggerChronicle
// 在 prefixHorizon 分支之外无条件调用（PluginTransforms.fs），链上注入与否由
// maybeInject 门禁的输入形状决定。companion blogger 会话在第 11 步
// ApplyEnforcerContinuation 分支：blogger session 是 companion satellite
// （tryMainSessionOf → Some main）→ handleContinuation 读 liveCtx =
// tryLiveCycleContext（只读 process-local InFlight flight，scope.TryPeekCurrentRequest，
// 绝不从 durable open 恢复）。live flight 在时 First-step 分支把 BloggerRequest
// canonical 视图整体替换 wire（message id replaced）——第 15 步门禁读的
// lastUserMessageId 变成渲染 id，tryReadExecution 无 lease → 静默 no-op。这就是
// R1/R3/R4/R6 旧 setup 的 0 注入根因：不是 maybeInject 没被调用，是 continuation
// 投影替换了门禁读取的 frontier。链上注入的真实形态（本批 setup）：
// journal Accepted（chat.message admission transaction）+ journal durable open
// BloggerRequest（plan freeze 的 companion 检查放行，claimBloggerRequest 落地后
// 立即 dispose——flight 释放使第 11 步 liveCtx None，wire 保持 raw frontier）+
// frontier physical 的 exact committed lease（B 系列同款 surface 直落；第八层已
// 实证 stub chat.message 的 physical binding 不落地，lease 不再依赖 chat.message）+
// 白名单模型。R5 保留 todo：typed boundary failure 经 MessagesTransform
// TypedPolicyFailClosed membrane 后的可观察形态（JS 异常或静默诊断返回）未实证。
// ---------------------------------------------------------------------------

const registeredUserMessage = (session, id) => ({
  info: { id, sessionID: session, role: 'user', model: { providerID: 'fixture', modelID: 'fixture-model' } },
  parts: [{ type: 'text', text: '材料已备好，请记账' }],
})

const appendCompanionBloggerLink = async (runtime, mainSession, bloggerSession) => {
  const linked = await JournalSurface.JournalSurface_appendAgent(
    runtime.journal,
    { kind: 'Session', session: mainSession },
    null,
    {
      family: 'Companion',
      case: 'CompanionBloggerLinked',
      payload: { SessionId: mainSession, BloggerSessionId: bloggerSession, BloggerAgent: 'blogger' },
    },
  )
  assert.ok(
    linked?.ok,
    `companion link append must succeed: ${linked?.error ?? 'unknown error'}`,
  )
}

// The registered transform runs the full normalTransform chain, whose
// HOST-BOUNDARY-008 step freezes the provider attempt plan only after a durable
// Accepted execution exists for the exact (session, physical) pair. The
// production entry for that evidence is acceptHumanRoot followed by the
// chat.message admission hook (same order as context-compression-018). The
// chat.message agent must match the lease participant ("chronicler", the same
// identity acquireLease commits) or the routing step rejects the drift.
//
// Eighth-layer root cause (fixture shape): when the HumanRoot opening physical
// equals the chat.message physical, the admission ingress classifies the
// message as a replay of the already-accepted root material, so the managed
// admission transaction (Accept → Acquire → Commit) never runs and
// ModelRouting.tryReadExecution(session, physical) stays null. Production
// keeps the root opening physical and each chat.message frontier physical as
// distinct messages — the context-compression-018 fixture does the same by
// letting the root default to `root-<session>` — so the admission here adopts
// that shape: the root physical stays distinct and the chat.message physical
// is the fresh frontier message whose admission acquires and commits the
// exact lease.
const admitExecution = async (runtime, hooks, session, physical) => {
  // sendContinuation (inside the BloggerRequest chain) only continues the
  // exact active run, so the caller needs the durable profile this root
  // installs on the Blogger session. The root physical defaults to
  // `root-<session>` and is deliberately distinct from the chat.message
  // frontier physical (see the eighth-layer note above).
  const profile = await acceptAuthorityRoot(runtime, session, 'blogger')
  await hooks['chat.message'](
    { sessionID: session, messageID: physical },
    {
      message: {
        id: physical,
        sessionID: session,
        role: 'user',
        model: { providerID: 'fixture', modelID: 'fixture-model' },
      },
      parts: [{ type: 'text', text: '材料已备好，请记账' }],
    },
  )
  return profile
}

const chronicleMarkers = (messages) =>
  messages.filter(
    (m) => m?.info?.role === 'assistant' && m?.parts?.length === 1 && m.parts[0]?.type === 'text' &&
      (m.parts[0]?.text === zhResource || m.parts[0]?.text === enResource),
  )

const hostHistoryContainsMarker = (runtime) =>
  runtime.messages.some((message) => {
    const text = JSON.stringify(message?.parts ?? [])
    return text.includes(zhResource) || text.includes(enResource)
  })

const journalTreeContainsMarker = (directory) => {
  const walk = (dir) => {
    for (const name of readdirSync(dir)) {
      const full = join(dir, name)
      if (statSync(full).isDirectory()) {
        if (name === '.git') continue
        if (walk(full)) return true
      } else {
        const text = readFileSync(full, 'utf8')
        if (text.includes(zhResource) || text.includes(enResource)) return true
      }
    }
    return false
  }
  return walk(directory)
}

const withEnglishLanguage = async (action) => {
  const previous = process.env.WANXIANGSHU_PROVIDER_LANGUAGE
  process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'
  LanguageSurface.refreshGlobalLanguage()
  try {
    await action()
  } finally {
    if (previous === undefined) {
      delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
    } else {
      process.env.WANXIANGSHU_PROVIDER_LANGUAGE = previous
    }
    LanguageSurface.refreshGlobalLanguage()
  }
}

test('WHAT[cognitive-environment-015] R1_registered_transform_injects_one_marker_and_writes_no_durable_history', { todo: 'tenth layer (this round): the acquireLease-surface-direct design is disproved — acquire adopts the chat.message session-scoped lease (kind Acquired), the manual commit returns AlreadyApplied (idempotent, chat.message already committed), and tryReadExecution(session, physical) stays null through all of it. The adopt path never writes the physical binding onto the session-scoped lease, and the fresh-reserve path is unreachable because the session lease pre-exists. Structural conclusion: under the stub chat.message shape the physical-bound lease the injection gate reads cannot be established at all — the production chat.message builds the physical binding directly on admission, which the stub does not replicate. Fixture admission-shape alignment (physical-binding admission entry) remains the open prerequisite. Assertions and setup preserved' }, async () => {
  await withEnglishLanguage(async () => {
    delete globalThis.__wanxiangshu_test_blogger_model
    await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
      const session = 'ses-blog-registered-1'
      const main = 'ses-blog-main-registered-1'
      const physical = 'msg-user-r1'
      await appendCompanionBloggerLink(runtime, main, session)
      // chat.message admission transaction writes the durable Accepted
      // execution (the plan freeze requires it); the exact lease is committed
      // through the ModelRouting surface below (eighth layer proved the stub
      // chat.message shape never lands the physical binding the injection
      // gate reads).
      const profile = await admitExecution(runtime, hooks, session, physical)
      // Durable BloggerRequest chain: the plan freeze companion check needs a
      // journal open request (BloggerRequestMaterialized). The live flight is
      // released immediately after the claim — tryLiveCycleContext reads only
      // the process-local InFlight (TryPeekCurrentRequest, never healed from
      // durable open), so step 11 keeps the raw wire frontier instead of
      // projecting the BloggerRequest canonical view (the ninth-layer root
      // cause of the old zero-injection runs).
      const bloggerRequest = await claimBloggerRequest({
        runtime,
        mainSession: main,
        bloggerSession: session,
        profile,
        dispatchPhysical: 'msg-dispatch-r1',
        requestId: 'req-blog-r1',
      })
      bloggerRequest.dispose()

      // The exact committed lease the injection model gate reads
      // (bloggerChronicleTextEnabled → ModelRouting.tryReadExecution), same
      // emr-011 surface path as B1—B5.
      await acquireLease(session, physical, 'blogger', 'blogger')

      // The provider start boundary needs a bindable Host run: the physical
      // user message plus an unsealed assistant child in the runtime snapshot
      // (same shape as context-compression-018's transform fixture). The
      // stubClient does not simulate the real Host publishing the assistant
      // when the provider run starts, so the pair is pushed explicitly.
      runtime.pushHostMessage(session, {
        info: { id: physical, sessionID: session, role: 'user', time: { created: 1 } },
        parts: [{ type: 'text', text: '材料已备好，请记账' }],
      })
      runtime.pushHostMessage(session, {
        info: {
          id: 'assistant-r1',
          sessionID: session,
          parentID: physical,
          role: 'assistant',
          agent: 'blogger',
          providerID: 'fixture',
          modelID: 'fixture-model',
          time: { created: 2 },
        },
        parts: [],
      })

      const outObj = { messages: [structuredClone(registeredUserMessage(session, physical))] }
      await hooks['experimental.chat.messages.transform']({ sessionID: session }, outObj)

      const markers = chronicleMarkers(outObj.messages)
      assert.equal(markers.length, 1, 'registered transform must inject exactly one chronicle marker')
      const marker = markers[0]
      assert.equal(marker.parts[0].text, enResource, 'marker text must be the en leaf under the global language binding')
      assert.ok(marker.info.id.startsWith('text-'), 'marker id must be digest-derived')
      const markerIndex = outObj.messages.indexOf(marker)
      assert.ok(markerIndex >= 0)
      assert.equal(
        outObj.messages[markerIndex + 1]?.info?.id,
        physical,
        'the marker must sit immediately before the frontier user message',
      )

      // WHAT 015：提示只作用于当次转换，不写入日志或历史。transform 修改的
      // 是 provider wire 投影 outObj；Host 持久历史（stubClient messages）与
      // journal 字节都不得出现 marker 文本。
      assert.equal(hostHistoryContainsMarker(runtime), false, 'Host persisted history must not contain the marker')
      assert.equal(journalTreeContainsMarker(directory), false, 'journal bytes must not contain the marker')
    })
  })
})

test('WHAT[cognitive-environment-015] R2_registered_transform_non_companion_session_injects_nothing', { todo: 'tenth layer (this round): the acquireLease-surface-direct design is disproved — acquire adopts the chat.message session-scoped lease (kind Acquired), the manual commit returns AlreadyApplied (idempotent, chat.message already committed), and tryReadExecution(session, physical) stays null through all of it. The adopt path never writes the physical binding onto the session-scoped lease, and the fresh-reserve path is unreachable because the session lease pre-exists. Structural conclusion: under the stub chat.message shape the physical-bound lease the injection gate reads cannot be established at all — the production chat.message builds the physical binding directly on admission, which the stub does not replicate. Fixture admission-shape alignment (physical-binding admission entry) remains the open prerequisite. Assertions and setup preserved' }, async () => {
  await withEnglishLanguage(async () => {
    delete globalThis.__wanxiangshu_test_blogger_model
    await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
      // A committed whitelisted lease but no CompanionBloggerLinked in the
      // journal: the session is not a companion, so the isCompanion half of
      // the injection gate must refuse — the only reason this case injects
      // nothing. The exact lease is committed through the ModelRouting
      // surface (ninth layer: the stub chat.message shape never lands the
      // physical binding), so the zero-injection verdict is attributable to
      // the companion gate alone, not a lease read miss.
      const session = 'ses-blog-registered-2'
      const physical = 'msg-user-r2'
      await admitExecution(runtime, hooks, session, physical)
      await acquireLease(session, physical, 'blogger', 'blogger')

      // Bindable Host run for the provider start boundary (same shape as
      // context-compression-018's transform fixture).
      runtime.pushHostMessage(session, {
        info: { id: physical, sessionID: session, role: 'user', time: { created: 1 } },
        parts: [{ type: 'text', text: '材料已备好，请记账' }],
      })
      runtime.pushHostMessage(session, {
        info: {
          id: 'assistant-r2',
          sessionID: session,
          parentID: physical,
          role: 'assistant',
          agent: 'blogger',
          providerID: 'fixture',
          modelID: 'fixture-model',
          time: { created: 2 },
        },
        parts: [],
      })

      const outObj = { messages: [structuredClone(registeredUserMessage(session, physical))] }
      await hooks['experimental.chat.messages.transform']({ sessionID: session }, outObj)

      assert.equal(chronicleMarkers(outObj.messages).length, 0, 'a non-companion session must receive no chronicle marker')
    })
  })
})

test('WHAT[cognitive-environment-015] R3_registered_transform_replays_same_occurrence_without_duplicate_markers', { todo: 'tenth layer (this round): the acquireLease-surface-direct design is disproved — acquire adopts the chat.message session-scoped lease (kind Acquired), the manual commit returns AlreadyApplied (idempotent, chat.message already committed), and tryReadExecution(session, physical) stays null through all of it. The adopt path never writes the physical binding onto the session-scoped lease, and the fresh-reserve path is unreachable because the session lease pre-exists. Structural conclusion: under the stub chat.message shape the physical-bound lease the injection gate reads cannot be established at all — the production chat.message builds the physical binding directly on admission, which the stub does not replicate. Fixture admission-shape alignment (physical-binding admission entry) remains the open prerequisite. Assertions and setup preserved' }, async () => {
  await withEnglishLanguage(async () => {
    delete globalThis.__wanxiangshu_test_blogger_model
    await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
      const session = 'ses-blog-registered-3'
      const main = 'ses-blog-main-registered-3'
      const physical = 'msg-user-r3'
      await appendCompanionBloggerLink(runtime, main, session)
      const profile = await admitExecution(runtime, hooks, session, physical)
      // Same shape as R1: the durable open request feeds the plan freeze
      // companion check; the live flight is disposed so step 11 keeps the raw
      // wire frontier (ninth-layer root cause fix).
      const bloggerRequest = await claimBloggerRequest({
        runtime,
        mainSession: main,
        bloggerSession: session,
        profile,
        dispatchPhysical: 'msg-dispatch-r3',
        requestId: 'req-blog-r3',
      })
      bloggerRequest.dispose()
      await acquireLease(session, physical, 'blogger', 'blogger')
      runtime.pushHostMessage(session, {
        info: { id: physical, sessionID: session, role: 'user', time: { created: 1 } },
        parts: [{ type: 'text', text: '材料已备好，请记账' }],
      })
      runtime.pushHostMessage(session, {
        info: {
          id: 'assistant-r3',
          sessionID: session,
          parentID: physical,
          role: 'assistant',
          agent: 'blogger',
          providerID: 'fixture',
          modelID: 'fixture-model',
          time: { created: 2 },
        },
        parts: [],
      })

      const outObj = { messages: [structuredClone(registeredUserMessage(session, physical))] }
      await hooks['experimental.chat.messages.transform']({ sessionID: session }, outObj)
      const first = chronicleMarkers(outObj.messages)
      assert.equal(first.length, 1, 'the first pass injects exactly one marker')

      // 同一 outObj（同一 occurrence 的重复 transform）：既有同文本 assistant
      // 消息被 filter 剔除后重新插入恰好一条，id 保持 digest 稳定。
      await hooks['experimental.chat.messages.transform']({ sessionID: session }, outObj)
      const replayed = chronicleMarkers(outObj.messages)
      assert.equal(replayed.length, 1, 'replay must not accumulate duplicate markers')
      assert.equal(replayed[0].info.id, first[0].info.id, 'the same occurrence keeps its digest-derived id stable')
      assert.equal(
        outObj.messages[outObj.messages.indexOf(replayed[0]) + 1]?.info?.id,
        physical,
        'the marker must stay immediately before the frontier user message after replay',
      )
    })
  })
})

test('WHAT[cognitive-environment-015] R4_registered_transform_followup_request_history_boundary', { todo: 'tenth layer (this round): the acquireLease-surface-direct design is disproved — acquire adopts the chat.message session-scoped lease (kind Acquired), the manual commit returns AlreadyApplied (idempotent, chat.message already committed), and tryReadExecution(session, physical) stays null through all of it. The adopt path never writes the physical binding onto the session-scoped lease, and the fresh-reserve path is unreachable because the session lease pre-exists. Structural conclusion: under the stub chat.message shape the physical-bound lease the injection gate reads cannot be established at all — the production chat.message builds the physical binding directly on admission, which the stub does not replicate. Fixture admission-shape alignment (physical-binding admission entry) remains the open prerequisite. Assertions and setup preserved' }, async () => {
  await withEnglishLanguage(async () => {
    delete globalThis.__wanxiangshu_test_blogger_model
    await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
      const session = 'ses-blog-registered-4'
      const main = 'ses-blog-main-registered-4'
      await appendCompanionBloggerLink(runtime, main, session)

      // 第一条物理 user 消息：Host 持久化它，插件为它建立 exact lease
      // （ninth layer：lease 经 ModelRouting surface 直落——stub chat.message
      // 的 physical binding 不落地）。
      const physicalOne = 'msg-user-r4-1'
      const userOne = registeredUserMessage(session, physicalOne)
      runtime.pushHostMessage(session, structuredClone(userOne))
      const profile = await admitExecution(runtime, hooks, session, physicalOne)
      // Same shape as R1: durable open request feeds the plan freeze; the
      // live flight is disposed so step 11 keeps the raw wire frontier.
      const bloggerRequest = await claimBloggerRequest({
        runtime,
        mainSession: main,
        bloggerSession: session,
        profile,
        dispatchPhysical: 'msg-dispatch-r4',
        requestId: 'req-blog-r4',
      })
      bloggerRequest.dispose()
      await acquireLease(session, physicalOne, 'blogger', 'blogger')

      // Bindable Host run for the first provider request's start boundary
      // (same shape as context-compression-018's transform fixture).
      runtime.pushHostMessage(session, {
        info: {
          id: 'assistant-r4-1',
          sessionID: session,
          parentID: physicalOne,
          role: 'assistant',
          agent: 'blogger',
          providerID: 'fixture',
          modelID: 'fixture-model',
          time: { created: 2 },
        },
        parts: [],
      })

      const firstRequest = { messages: [structuredClone(userOne)] }
      await hooks['experimental.chat.messages.transform']({ sessionID: session }, firstRequest)
      const firstMarkers = chronicleMarkers(firstRequest.messages)
      assert.equal(firstMarkers.length, 1, 'the first provider request carries its marker')
      const firstMarkerId = firstMarkers[0].info.id

      // 注入没有污染任何持久位置：Host 历史与 journal 都不含 marker 文本。
      assert.equal(hostHistoryContainsMarker(runtime), false, 'Host persisted history stays free of the injected marker')
      assert.equal(journalTreeContainsMarker(directory), false, 'journal bytes stay free of the injected marker')

      // 后续请求：Host 从持久历史构建 wire 输入——历史里只有原始 user 消息，
      // 上一次注入的 marker 不出现在下一请求的输入中。
      const physicalTwo = 'msg-user-r4-2'
      const userTwo = registeredUserMessage(session, physicalTwo)
      await admitExecution(runtime, hooks, session, physicalTwo)
      await acquireLease(session, physicalTwo, 'blogger', 'blogger')
      // Bindable Host run for the follow-up request's start boundary.
      runtime.pushHostMessage(session, structuredClone(userTwo))
      runtime.pushHostMessage(session, {
        info: {
          id: 'assistant-r4-2',
          sessionID: session,
          parentID: physicalTwo,
          role: 'assistant',
          agent: 'blogger',
          providerID: 'fixture',
          modelID: 'fixture-model',
          time: { created: 4 },
        },
        parts: [],
      })

      const secondRequest = { messages: [structuredClone(userOne), structuredClone(userTwo)] }
      assert.equal(
        chronicleMarkers(secondRequest.messages).length,
        0,
        'the next request input must not carry the previous request marker',
      )

      await hooks['experimental.chat.messages.transform']({ sessionID: session }, secondRequest)
      const secondMarkers = chronicleMarkers(secondRequest.messages)
      assert.equal(secondMarkers.length, 1, 'the follow-up request injects exactly one fresh marker')
      assert.notEqual(secondMarkers[0].info.id, firstMarkerId, 'a new occurrence derives a new marker id')
      const markerIndex = secondRequest.messages.indexOf(secondMarkers[0])
      assert.equal(
        secondRequest.messages[markerIndex + 1]?.info?.id,
        physicalTwo,
        'the fresh marker precedes the new frontier user message',
      )
      assert.equal(
        secondRequest.messages[markerIndex - 1]?.info?.id,
        physicalOne,
        'the earlier physical user message remains ahead of the fresh marker',
      )
    })
  })
})

test('WHAT[cognitive-environment-015] R5_registered_transform_without_committed_lease_fails_closed_with_typed_rejection', { todo: 'ninth-layer adjudication: setup now isolates the exact committed lease (durable Accepted stays via the chat.message admission transaction, journal open BloggerRequest feeds the plan freeze with the live flight disposed, the exact lease is committed through the ModelRouting surface and then settled away with releasePhysical), so the provider start boundary requireProviderAdmission must read no committed lease and reject with the CommittedAdmissionUnavailable typed failure. Open gap: the MessagesTransform hook is registered TypedPolicyFailClosed (HookPolicy), and the observable shape of that typed boundary failure at the hook boundary — a JS exception surfaced to this test, or a quiet typed-failure return swallowed by the membrane — has not been proven by a real run. The assertion intent stays: fail closed, never a quiet zero-injection completion. Assertions and setup preserved' }, async () => {
  await withEnglishLanguage(async () => {
    delete globalThis.__wanxiangshu_test_blogger_model
    await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
      const session = 'ses-blog-registered-5'
      const main = 'ses-blog-main-registered-5'
      await appendCompanionBloggerLink(runtime, main, session)

      // companion 成立但没有 exact committed lease：host-boundary-008 的模型
      // 门禁读不到本请求的模型身份，绝不借用其它执行的 current model。
      // Accepted 执行仍先建立——被测的是 lease 缺席，不是 Accepted 缺席。
      const physical = 'msg-user-r5'
      const profile = await admitExecution(runtime, hooks, session, physical)
      // Same shape as R1: the durable open request feeds the plan freeze
      // companion check; the live flight is disposed so step 11 keeps the raw
      // wire frontier.
      const bloggerRequest = await claimBloggerRequest({
        runtime,
        mainSession: main,
        bloggerSession: session,
        profile,
        dispatchPhysical: 'msg-dispatch-r5',
        requestId: 'req-blog-r5',
      })
      bloggerRequest.dispose()

      // Bindable Host run for the provider start boundary (same shape as R1);
      // the pair is present so the only missing precondition is the committed
      // lease itself.
      runtime.pushHostMessage(session, {
        info: { id: physical, sessionID: session, role: 'user', time: { created: 1 } },
        parts: [{ type: 'text', text: '材料已备好，请记账' }],
      })
      runtime.pushHostMessage(session, {
        info: {
          id: 'assistant-r5',
          sessionID: session,
          parentID: physical,
          role: 'assistant',
          agent: 'blogger',
          providerID: 'fixture',
          modelID: 'fixture-model',
          time: { created: 2 },
        },
        parts: [],
      })

      // Ninth-layer adjudication: commit the exact lease through the
      // ModelRouting surface (the production emr-011 path), then settle it
      // away on the capacity owner (durable Accepted stays in the journal);
      // the provider start boundary's requireProviderAdmission must reject
      // the transform with the CommittedAdmissionUnavailable typed failure —
      // fail-closed, not a quiet zero-injection completion.
      await acquireLease(session, physical, 'blogger', 'blogger')
      const released = ModelRoutingSurface.releasePhysical(session, physical)
      assert.ok(
        released?.kind === 'Applied' || released?.kind === 'AlreadyApplied',
        'the exact lease must be settled away before the transform runs',
      )

      const outObj = { messages: [structuredClone(registeredUserMessage(session, physical))] }
      await assert.rejects(
        () => hooks['experimental.chat.messages.transform']({ sessionID: session }, outObj),
        (error) => {
          assert.match(
            String(error?.message ?? error),
            /committed-admission-unavailable/,
            'the rejection must be the CommittedAdmissionUnavailable typed boundary failure',
          )
          return true
        },
        'a companion transform without a committed exact lease must fail closed instead of completing quietly',
      )
      assert.equal(chronicleMarkers(outObj.messages).length, 0, 'a rejected transform must inject no chronicle marker')
    })
  })
})

test('WHAT[cognitive-environment-015] R6_registered_transform_non_whitelisted_model_injects_nothing', { todo: 'tenth layer (this round): the acquireLease-surface-direct design is disproved — acquire adopts the chat.message session-scoped lease (kind Acquired), the manual commit returns AlreadyApplied (idempotent, chat.message already committed), and tryReadExecution(session, physical) stays null through all of it. The adopt path never writes the physical binding onto the session-scoped lease, and the fresh-reserve path is unreachable because the session lease pre-exists. Structural conclusion: under the stub chat.message shape the physical-bound lease the injection gate reads cannot be established at all — the production chat.message builds the physical binding directly on admission, which the stub does not replicate. Fixture admission-shape alignment (physical-binding admission entry) remains the open prerequisite. Assertions and setup preserved' }, async () => {
  await withEnglishLanguage(async () => {
    globalThis.__wanxiangshu_test_blogger_model = 'test/other-model'
    try {
      await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
        const session = 'ses-blog-registered-6'
        const main = 'ses-blog-main-registered-6'
        const physical = 'msg-user-r6'
        await appendCompanionBloggerLink(runtime, main, session)
        const profile = await admitExecution(runtime, hooks, session, physical)
        // Same shape as R1: durable open request feeds the plan freeze; the
        // live flight is disposed so step 11 keeps the raw wire frontier.
        const bloggerRequest = await claimBloggerRequest({
          runtime,
          mainSession: main,
          bloggerSession: session,
          profile,
          dispatchPhysical: 'msg-dispatch-r6',
          requestId: 'req-blog-r6',
        })
        bloggerRequest.dispose()
        // The exact lease is committed through the ModelRouting surface; the
        // scheduler routes this admission to the non-whitelisted model via
        // __wanxiangshu_test_blogger_model, so the whitelist half of the
        // injection gate is the only reason this case injects nothing.
        await acquireLease(session, physical)

      const outObj = { messages: [structuredClone(registeredUserMessage(session, physical))] }
      // The provider start boundary needs a bindable Host run: the physical
      // user message plus an unsealed assistant child in the runtime snapshot
      // (same shape as context-compression-018's transform fixture).
      runtime.pushHostMessage(session, {
        info: { id: physical, sessionID: session, role: 'user', time: { created: 1 } },
        parts: [{ type: 'text', text: '材料已备好，请记账' }],
      })
      runtime.pushHostMessage(session, {
        info: {
          id: 'assistant-r1',
          sessionID: session,
          parentID: physical,
          role: 'assistant',
          agent: 'blogger',
          providerID: 'fixture',
          modelID: 'fixture-model',
          time: { created: 2 },
        },
        parts: [],
      })
      await hooks['experimental.chat.messages.transform']({ sessionID: session }, outObj)

        assert.equal(chronicleMarkers(outObj.messages).length, 0, 'a non-whitelisted lease target must not inject')
      })
    } finally {
      delete globalThis.__wanxiangshu_test_blogger_model
    }
  })
})
