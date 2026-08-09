import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'

import { awaitPrompted, withExecutablePlugin } from '../plugin/plugin-fixture.mjs'

const toolContext = (sessionID, messageID, callID) => ({
  sessionID,
  messageID,
  callID,
  abort: new AbortController().signal,
})

const promptKeyOf = (prompt) =>
  prompt?.body?.metadata?.wanxiangshu_prompt_key ??
  prompt?.body?.parts?.[0]?.metadata?.wanxiangshu_prompt_key

const assistantMessage = (sessionID, id, parentID, agent, text) => ({
  info: {
    id,
    role: 'assistant',
    sessionID,
    parentID,
    agent,
    finish: 'stop',
    time: { completed: Date.now() },
  },
  parts: [{ type: 'text', text }],
})

const idle = (sessionID) => ({
  type: 'session.status',
  properties: { sessionID, status: { type: 'idle' } },
})

// EXEC_025 hang root cause (harness): waiting on prompts.length without a budget.
// When a Teacher execute does not grow `runtime.prompts` (e.g. in-flight reject),
// an unbounded loop spins until the runner ~30s timeout. Bounded helper fails fast.
const awaitPromptGrowth = async (prompts, index, session, budgetMs = 3000) => {
  const timeoutMs =
    budgetMs != null && typeof budgetMs === 'object'
      ? (budgetMs.timeoutMs ?? budgetMs.budgetMs ?? 3000)
      : budgetMs
  const deadline = Date.now() + timeoutMs
  while (prompts.length <= index) {
    if (Date.now() >= deadline) {
      throw new Error(
        `prompt[${index}] for session ${session} did not arrive within ${timeoutMs}ms (prompts.length=${prompts.length})`,
      )
    }
    await new Promise((resolve) => setImmediate(resolve))
  }
}

test('EXEC_025_awaitPromptGrowth_fails_fast_when_prompts_never_grow', async () => {
  const BOUND_MS = 100
  let helperError
  const outcome = await Promise.race([
    awaitPromptGrowth([], 0, 'ses_red', BOUND_MS).then(
      () => 'grew',
      (err) => {
        helperError = err
        return 'rejected'
      },
    ),
    new Promise((resolve) => setTimeout(() => resolve('hung'), BOUND_MS + 150)),
  ])
  assert.equal(
    outcome,
    'rejected',
    `awaitPromptGrowth must honor timeoutMs and fail fast when prompts never grow; got ${outcome}`,
  )
  assert.match(
    String(helperError?.message ?? helperError),
    /did not arrive within/,
    'fail-fast error must name the missing prompt growth',
  )
})

test('EXEC_025_prompt_growth_wait_must_be_bounded', () => {
  const source = readFileSync(new URL(import.meta.url), 'utf8')
  assert.match(source, /const awaitPromptGrowth\s*=/, 'awaitPromptGrowth helper must exist')
  assert.match(
    source,
    /EXEC_025_three_teacher_calls_reuse_one_private_session_and_QA_records_raw_order[\s\S]*?await awaitPromptGrowth\(/,
    'three_teacher must wait via awaitPromptGrowth',
  )

  const helperMatch = source.match(
    /const awaitPromptGrowth\s*=\s*async\s*\([^)]*\)\s*=>\s*\{([\s\S]*?)\n\}/,
  )
  assert.ok(helperMatch, 'awaitPromptGrowth body must be parseable')
  const helperBody = helperMatch[1]
  assert.match(
    helperBody,
    /timeoutMs|budgetMs|deadline/,
    'awaitPromptGrowth must enforce a timeout budget/deadline',
  )
  assert.doesNotMatch(
    helperBody,
    /\bvoid\s+(budgetMs|timeoutMs)\b/,
    'budget must not be discarded',
  )

  // Detect truly unbounded busy-waits outside the helper: while + setImmediate/setTimeout
  // without a nearby timeoutMs/budgetMs/deadline. Helper internals are excluded so a
  // bounded poll does not false-positive.
  const outsideHelper = source.replace(helperMatch[0], '')
  const busyWaitBlocks = [
    ...outsideHelper.matchAll(/while\s*\([^)]*\)\s*\{[\s\S]*?\}/g),
  ].map((m) => m[0])
  const unbounded = busyWaitBlocks.some(
    (block) =>
      /prompts\.length/.test(block) &&
      /await\s+new\s+Promise[\s\S]*(?:setImmediate|setTimeout)/.test(block) &&
      !/(timeoutMs|budgetMs|deadline)/.test(block),
  )
  assert.equal(
    unbounded,
    false,
    'EXEC_025 must not unbounded-wait on runtime.prompts.length; use awaitPromptGrowth(..., { timeoutMs }) that fails fast when prompts do not grow',
  )
})

test('EXEC_025_three_teacher_calls_reuse_one_private_session_and_QA_records_raw_order', async () => {
  await withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
    const student = 'ses_student_tool_loop'
    const opening = '请帮我从第一性原理学习这个主题。'

    await hooks['chat.message'](
      { sessionID: student, agent: 'fast-student', messageID: 'msg_student_root' },
      {
        message: { id: 'msg_student_root', role: 'user', sessionID: student, agent: 'fast-student' },
        parts: [{ type: 'text', text: opening }],
      },
    )

    const exchanges = [
      ['第一个问题', '第一个回答'],
      ['第二个问题', '第二个回答'],
      ['第三个问题', '第三个回答'],
    ]

    for (let index = 0; index < exchanges.length; index += 1) {
      const [question, answer] = exchanges[index]
      const studentRun = hooks.tool.teacher.execute(
        { message: question },
        toolContext(student, `asst_student_${index}`, `call_teacher_${index}`),
      )

      await awaitPromptGrowth(runtime.prompts, index, student)
      const teacher = createdIds[0]
      assert.ok(teacher, 'first call must create the private Teacher')
      await awaitPrompted(teacher)

      const prompt = runtime.prompts[index]
      const promptKey = promptKeyOf(prompt)
      assert.ok(promptKey, 'Teacher prompt must carry PromptDispatcher identity')
      assert.equal(prompt.body.agent, 'fast-teacher')
      assert.equal(prompt.body.model, undefined, 'Teacher model must come from its Host agent config')
      assert.equal(prompt.body.tools.return, true)
      assert.equal(prompt.body.tools.read, true)
      assert.equal(prompt.body.tools.fork, false)
      assert.equal(prompt.body.tools.join, false)
      assert.equal(prompt.body.tools['fork-pty'], false)

      await hooks['chat.message'](
        { sessionID: teacher, agent: 'fast-teacher', messageID: `msg_teacher_${index}` },
        {
          message: {
            id: `msg_teacher_${index}`,
            role: 'user',
            sessionID: teacher,
            agent: 'fast-teacher',
          },
          parts: [
            {
              type: 'text',
              text: prompt.body.parts[0].text,
              metadata: { wanxiangshu_prompt_key: promptKey },
            },
          ],
        },
      )
      runtime.pushHostMessage(teacher, {
        info: {
          id: `msg_teacher_${index}`,
          role: 'user',
          sessionID: teacher,
          agent: 'fast-teacher',
          metadata: { wanxiangshu_prompt_key: promptKey },
        },
        parts: [
          {
            type: 'text',
            text: prompt.body.parts[0].text,
            metadata: { wanxiangshu_prompt_key: promptKey },
          },
        ],
      })

      const teacherReturn = await hooks.tool.return.execute(
        { message: answer },
        toolContext(teacher, `asst_teacher_${index}`, `call_return_${index}`),
      )
      assert.equal(parseToml(teacherReturn).completion_text, 'Teacher answer returned to Student.')

      const completionID = `asst_teacher_completion_${index}`
      const completion = { text: 'provider trailing prose' }
      await hooks['experimental.text.complete'](
        { sessionID: teacher, messageID: completionID, partID: `part_teacher_completion_${index}` },
        completion,
      )
      assert.equal(completion.text, 'Teacher answer returned to Student.')
      runtime.pushHostMessage(
        teacher,
        assistantMessage(teacher, completionID, `msg_teacher_${index}`, 'fast-teacher', completion.text),
      )
      hooks.event(idle(teacher))

      const delivered = parseToml(await studentRun)
      assert.equal(delivered.answer, answer)
      assert.equal(createdIds.length, 1, 'all calls must reuse exactly one Teacher Session')
      assert.deepEqual(runtime.abortedIds, [], 'successful Teacher returns must never abort the Session')
    }

    const privateGitDir = execFileSync(
      'git',
      ['-C', directory, 'rev-parse', '--absolute-git-dir'],
      { encoding: 'utf8' },
    ).trim()
    const gitDir = join(privateGitDir, 'wanxiangshu', 'student', student)
    const runDirectories = readdirSync(gitDir)
    assert.equal(runDirectories.length, 1)
    const qa = readFileSync(join(gitDir, runDirectories[0], 'QA.md'), 'utf8')
    assert.equal(
      qa,
      [opening, ...exchanges.flatMap(([question, answer]) => [question, answer])].join('\n\n'),
    )
  })
})
