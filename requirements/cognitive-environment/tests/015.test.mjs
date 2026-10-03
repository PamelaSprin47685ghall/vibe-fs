import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { mkdirSync, mkdtempSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import * as BloggerChronicleSurface from '../../../dist/OpenCode/Host/BloggerChronicleSurface.js'
import * as ModelRoutingSurface from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'

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

const acquireLease = async (session, physicalUserMessageId, role = 'blogger') => {
  const acquisition = await ModelRoutingSurface.acquireSharedExecutionAdmission(
    session,
    physicalUserMessageId,
    role,
    'chronicler',
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
