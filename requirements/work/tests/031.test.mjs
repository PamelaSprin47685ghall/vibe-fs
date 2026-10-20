import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import * as prompts from '../../../dist/Resources/PromptSurface.js'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const rootDir = path.resolve(__dirname, '../../..')
const resourcesDir = path.join(rootDir, 'resources/provider')

test('WHAT[work-031] plan role, stages, and tool resources exist in paired English and Simplified Chinese markdown files', () => {
  // The human-question resources have paired languages.
  const resourceBases = [
    'role/plan',
    'planning/s1',
    'planning/s2',
    'planning/s3',
    'tool/js-plan',
    'tool/ask',
    'tool/ask-question',
    'tool/ask-title',
    'tool/ask-sent',
    'tool/ask-failed',
    'tool/ask-waiting',
    'work/manager-continue',
    'work/plan-continue',
    'tool/review',
    'tool/deliver',
  ]

  for (const base of resourceBases) {
    const enFile = path.join(resourcesDir, base, 'en.md')
    const zhFile = path.join(resourcesDir, base, 'zh-CN.md')

    // Both language files must exist on disk
    assert.ok(fs.existsSync(enFile), `Expected ${base}/en.md to exist`)
    assert.ok(fs.existsSync(zhFile), `Expected ${base}/zh-CN.md to exist`)

    // Both files must have non-empty content
    const enContent = fs.readFileSync(enFile, 'utf8').trim()
    const zhContent = fs.readFileSync(zhFile, 'utf8').trim()

    assert.ok(enContent.length > 0, `${base}/en.md must not be empty`)
    assert.ok(zhContent.length > 0, `${base}/zh-CN.md must not be empty`)
  }

  // Retired plan tools must not keep loadable tool resources
  for (const retired of ['tool/resume', 'tool/handoff', 'tool/devops']) {
    const enFile = path.join(resourcesDir, retired, 'en.md')
    const zhFile = path.join(resourcesDir, retired, 'zh-CN.md')
    assert.ok(!fs.existsSync(enFile), `${retired}/en.md must not exist: the tool is retired from the plan surface`)
    assert.ok(!fs.existsSync(zhFile), `${retired}/zh-CN.md must not exist: the tool is retired from the plan surface`)
  }
})

test('WHAT[work-031] the prompt catalog carries a localized PlanSystemPrompt for real system transform wiring', () => {
  // WHAT[work-031]: ProviderSystemTransform must really assemble the plan
  // prompt; the catalog is the assembly source, so it must expose the plan
  // system prompt in both languages.
  for (const language of ['en', 'zh-CN']) {
    const catalog = prompts.loadForLanguage(language)
    assert.ok(catalog != null, `loadForLanguage(${language}) must return a catalog`)
    const planPrompt = catalog.PlanSystemPrompt
    assert.ok(
      typeof planPrompt === 'string' && planPrompt.trim().length > 0,
      `the ${language} prompt catalog must carry a non-empty PlanSystemPrompt`,
    )
  }
})

test('WHAT[work-031] plan prompts state that S1/S2 review gaps are entry-bar gaps to start the next stage and promoted reviews return the next stage method immediately', () => {
  const roleEn = fs.readFileSync(path.join(resourcesDir, 'role/plan/en.md'), 'utf8')
  const roleZh = fs.readFileSync(path.join(resourcesDir, 'role/plan/zh-CN.md'), 'utf8')
  const s1En = fs.readFileSync(path.join(resourcesDir, 'planning/s1/en.md'), 'utf8')
  const s1Zh = fs.readFileSync(path.join(resourcesDir, 'planning/s1/zh-CN.md'), 'utf8')
  const s2En = fs.readFileSync(path.join(resourcesDir, 'planning/s2/en.md'), 'utf8')
  const s2Zh = fs.readFileSync(path.join(resourcesDir, 'planning/s2/zh-CN.md'), 'utf8')
  const s3En = fs.readFileSync(path.join(resourcesDir, 'planning/s3/en.md'), 'utf8')
  const s3Zh = fs.readFileSync(path.join(resourcesDir, 'planning/s3/zh-CN.md'), 'utf8')
  const deliverEn = fs.readFileSync(path.join(resourcesDir, 'tool/deliver/en.md'), 'utf8')
  const deliverZh = fs.readFileSync(path.join(resourcesDir, 'tool/deliver/zh-CN.md'), 'utf8')
  const promotedEn = fs.readFileSync(path.join(resourcesDir, 'tool/review/plan-promoted/en.md'), 'utf8')
  const promotedZh = fs.readFileSync(path.join(resourcesDir, 'tool/review/plan-promoted/zh-CN.md'), 'utf8')

  // S1 gap is the gap to promotion to S2 (so S2 can start), not whether S2 paths are already written
  assert.match(s1En, /promotion to `S2`[\s\S]*`S2` can begin/i)
  assert.match(s1Zh, /能晋级 `S2`[\s\S]*`S2` 能开始/)

  // S2 gap is the gap to promotion to S3 (so S3 can start), not the gap to S3 completion
  assert.match(s2En, /promotion to `S3`[\s\S]*`S3` can begin[\s\S]*not the gap to `S3` completion/i)
  assert.match(s2Zh, /能晋级 `S3`[\s\S]*`S3` 能开始[\s\S]*而不是 `S3` 完成的差距/)

  // Role law and plan-promoted state that review immediately returns the next stage requirements
  assert.match(roleEn, /`review` tool result immediately returns the next stage's method requirements/i)
  assert.match(roleZh, /`review` 的工具返回值会当场给出下一阶段的方法要求/)
  assert.doesNotMatch(roleEn, /The one who works in the next stage is some later runner, not you/i)
  assert.doesNotMatch(roleZh, /进入下一阶段工作的是你之后的某任，不是你/)
  assert.doesNotMatch(promotedEn, /will be presented on the next request/i)
  assert.doesNotMatch(promotedZh, /将在下一次请求时呈现/)

  // S3 deliver enters the post-S3 state, stops nudging the retired run, interrupts like ask, and allows repeated S4 starts on user command.
  // The sentence carrying this clause is rewritten across prompt revisions, so the assertions
  // below check the semantics inside the post-S3 clauses instead of pinning one English or
  // Chinese wording: the retired run is no longer nudged, the conversation is interrupted like
  // ask, and the user may repeatedly request starting `S4`.
  const postS3Paragraphs = (text) => text.split(/\n{2,}/).filter((p) => p.includes('`post-S3`'))

  const assertPostS3Semantics = (text, label, wording) => {
    const paragraphs = postS3Paragraphs(text)
    assert.ok(paragraphs.length > 0, `${label}: must describe the post-S3 state`)
    const statesInPostS3 = (re) => paragraphs.some((p) => re.test(p))
    assert.ok(
      statesInPostS3(wording.noNudge),
      `${label}: the post-S3 clause must state that the retired run is no longer nudged`,
    )
    assert.ok(
      statesInPostS3(wording.interruptsLikeAsk),
      `${label}: the post-S3 clause must state that it interrupts the conversation like ask`,
    )
    assert.ok(
      statesInPostS3(wording.repeatedS4Start),
      `${label}: the post-S3 clause must state that the user may repeatedly request starting \`S4\``,
    )
  }

  const englishPostS3Wording = {
    noNudge: /neither nudged nor revived|no longer nudged|permanently disables? nudges?|not nudged|never nudged/i,
    interruptsLikeAsk: /interrupts?[^.\n]{0,120}\bask\b/i,
    repeatedS4Start: /repeatedly (?:request|ask|command)[^.\n]{0,80}`S4`/i,
  }

  const chinesePostS3Wording = {
    noNudge: /不再被催促|不再催促|永久关闭[^。]{0,20}催促|不受催促|不再被自动催促/,
    interruptsLikeAsk: /像\s*`?ask`?\s*一样中断|中断[^。]{0,30}(?:当前)?对话/,
    repeatedS4Start: /反复[^。]{0,30}启动 `S4`/,
  }

  assertPostS3Semantics(roleEn, 'role/plan/en.md', englishPostS3Wording)
  assertPostS3Semantics(roleZh, 'role/plan/zh-CN.md', chinesePostS3Wording)
  assertPostS3Semantics(s3En, 'planning/s3/en.md', englishPostS3Wording)
  assertPostS3Semantics(s3Zh, 'planning/s3/zh-CN.md', chinesePostS3Wording)
  assertPostS3Semantics(deliverEn, 'tool/deliver/en.md', englishPostS3Wording)
  assertPostS3Semantics(deliverZh, 'tool/deliver/zh-CN.md', chinesePostS3Wording)

  // The repeated user-requested `S4` start is also pinned verbatim in the stage and tool prompts
  assert.match(s3En, /`post-S3`[\s\S]*repeatedly request starting `S4`/i)
  assert.match(s3Zh, /`post-S3`[\s\S]*反复要求启动 `S4`/)
  assert.match(deliverEn, /`post-S3`[\s\S]*repeatedly request starting `S4`/i)
  assert.match(deliverZh, /`post-S3`[\s\S]*反复要求启动 `S4`/)
})
