import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { existsSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'

import { DefaultAutoRecoveryBudget } from '../../../dist/Domain/AgentPairCursor.js'
import { awaitPrompted, withExecutablePlugin } from '../plugin/plugin-fixture.mjs'

const waitFor = async (predicate, message, budgetMs = 3000) => {
  const deadline = Date.now() + budgetMs
  while (!predicate()) {
    if (Date.now() >= deadline) throw new Error(message)
    await new Promise((resolve) => setImmediate(resolve))
  }
}

const toolContext = (sessionID, messageID, callID) => ({
  sessionID,
  messageID,
  callID,
  abort: new AbortController().signal,
})

const userMessage = (sessionID, id, agent, text, metadata) => ({
  info: { id, role: 'user', sessionID, agent, metadata },
  parts: [{ type: 'text', text, metadata }],
})

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

const acceptTeacherPrompt = async (hooks, runtime, teacher, prompt, rootID) => {
  const promptKey = prompt.body.metadata.wanxiangshu_prompt_key
  await hooks['chat.message'](
    { sessionID: teacher, agent: 'fast-teacher', messageID: rootID },
    {
      message: { id: rootID, role: 'user', sessionID: teacher, agent: 'fast-teacher' },
      parts: [
        {
          type: 'text',
          text: prompt.body.parts[0].text,
          metadata: { wanxiangshu_prompt_key: promptKey },
        },
      ],
    },
  )
  runtime.pushHostMessage(
    teacher,
    userMessage(teacher, rootID, 'fast-teacher', prompt.body.parts[0].text, {
      wanxiangshu_prompt_key: promptKey,
    }),
  )
}

const startStudent = async (hooks, student, rootID, text) => {
  await hooks['chat.message'](
    { sessionID: student, agent: 'fast-student', messageID: rootID },
    {
      message: { id: rootID, role: 'user', sessionID: student, agent: 'fast-student' },
      parts: [{ type: 'text', text }],
    },
  )
}

test('EXEC_027_concurrent_second_teacher_call_is_rejected_while_first_is_in_flight', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    const student = 'ses_student_ce_single_flight'
    await startStudent(hooks, student, 'msg_ce_sf_root', '单飞校验')

    const first = hooks.tool.teacher.execute(
      { message: '第一个问题' },
      toolContext(student, 'asst_ce_sf_1', 'call_ce_sf_1'),
    )
    await waitFor(() => createdIds.length === 1 && runtime.prompts.length === 1, 'first teacher prompt missing')

    const second = parseToml(
      await hooks.tool.teacher.execute(
        { message: '并发第二问' },
        toolContext(student, 'asst_ce_sf_2', 'call_ce_sf_2'),
      ),
    )
    assert.match(second.error, /in flight/)

    // Cleanup: cancel so the first waiter does not leak across the fixture.
    hooks.event({ type: 'session.deleted', properties: { sessionID: student } })
    const firstResult = parseToml(await first)
    assert.match(firstResult.error, /cancelled/)
  })
})

test('EXEC_027_dispose_fails_unsettled_teacher_call_scope', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    const student = 'ses_student_ce_dispose'
    await startStudent(hooks, student, 'msg_ce_dispose_root', 'dispose 校验')

    const parentTool = hooks.tool.teacher.execute(
      { message: '等待中被 dispose' },
      toolContext(student, 'asst_ce_dispose', 'call_ce_dispose'),
    )
    await waitFor(() => createdIds.length === 1 && runtime.prompts.length === 1, 'teacher prompt missing')

    await hooks.dispose()
    const result = parseToml(await parentTool)
    assert.match(result.error, /disposed|cancelled/i)
  })
})

test('EXEC_027_duplicate_return_is_rejected_and_completion_redelivery_is_idempotent', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    const student = 'ses_student_ce_idempotent'
    await startStudent(hooks, student, 'msg_ce_id_root', '幂等校验')

    const parentTool = hooks.tool.teacher.execute(
      { message: '请回答' },
      toolContext(student, 'asst_ce_id_wait', 'call_ce_id'),
    )
    await waitFor(() => runtime.prompts.length === 1, 'teacher prompt missing')
    const teacher = createdIds[0]
    await awaitPrompted(teacher)
    await acceptTeacherPrompt(hooks, runtime, teacher, runtime.prompts[0], 'msg_ce_id_teacher_root')

    const firstReturn = parseToml(
      await hooks.tool.return.execute(
        { message: '答案一次' },
        toolContext(teacher, 'asst_ce_id_tool', 'call_ce_id_return'),
      ),
    )
    assert.equal(firstReturn.completion_text, 'Teacher answer returned to Student.')

    const duplicate = parseToml(
      await hooks.tool.return.execute(
        { message: '答案两次' },
        toolContext(teacher, 'asst_ce_id_tool_dup', 'call_ce_id_return_dup'),
      ),
    )
    assert.match(duplicate.error, /already pending/)

    const completionOutput = { text: 'provider trailing prose' }
    await hooks['experimental.text.complete'](
      { sessionID: teacher, messageID: 'asst_ce_id_complete', partID: 'part_ce_id_complete' },
      completionOutput,
    )
    assert.equal(completionOutput.text, 'Teacher answer returned to Student.')

    // Redeliver the same TextComplete — must remain the fixed payload, not throw.
    const again = { text: 'another trailing prose' }
    await hooks['experimental.text.complete'](
      { sessionID: teacher, messageID: 'asst_ce_id_complete_2', partID: 'part_ce_id_complete_2' },
      again,
    )
    assert.equal(again.text, 'Teacher answer returned to Student.')

    runtime.pushHostMessage(
      teacher,
      assistantMessage(
        teacher,
        'asst_ce_id_complete',
        'msg_ce_id_teacher_root',
        'fast-teacher',
        'Teacher answer returned to Student.',
      ),
    )
    hooks.event(idle(teacher))
    assert.equal(parseToml(await parentTool).answer, '答案一次')
  })
})

test(
  'EXEC_027_payload_mismatch_nudges_until_budget_exhausts_parent_teacher',
  { timeout: 120_000 },
  async () => {
    await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
      const student = 'ses_student_ce_budget'
      await startStudent(hooks, student, 'msg_ce_budget_root', '预算校验')

      let settled = false
      const parentTool = hooks.tool.teacher
        .execute({ message: '需要预算' }, toolContext(student, 'asst_ce_budget', 'call_ce_budget'))
        .finally(() => {
          settled = true
        })

      await waitFor(() => runtime.prompts.length === 1, 'teacher prompt missing')
      const teacher = createdIds[0]
      await awaitPrompted(teacher)

      assert.equal(typeof DefaultAutoRecoveryBudget, 'number')
      assert.ok(DefaultAutoRecoveryBudget >= 1)

      // Production wiring proof: exhaustion path is not a dead string.
      const runtimeSource = await import('node:fs').then((fs) =>
        fs.readFileSync(new URL('../../../dist/Session/StudentTeacherRuntime.js', import.meta.url), 'utf8'),
      )
      assert.match(runtimeSource, /DefaultAutoRecoveryBudget/)
      assert.match(runtimeSource, /budget exhausted/i)

      for (let i = 0; i < DefaultAutoRecoveryBudget; i += 1) {
        const asst = `asst_ce_budget_plain_${i}`
        const root = `msg_ce_budget_cycle_${i}`
        const prompt = runtime.prompts[runtime.prompts.length - 1]
        await acceptTeacherPrompt(hooks, runtime, teacher, prompt, root)

        // HOST-004: a new provider attempt is required before the next idle can
        // mint a consumable permit (IdleConsumed does not roll back on ObserveIdle).
        await hooks['experimental.chat.messages.transform'](
          { sessionID: teacher },
          {
            messages: [
              {
                info: { id: root, role: 'user', sessionID: teacher, agent: 'fast-teacher' },
                parts: [{ type: 'text', text: prompt.body.parts[0].text }],
              },
            ],
          },
        )

        runtime.pushHostMessage(
          teacher,
          assistantMessage(teacher, asst, root, 'fast-teacher', '  plain prose  '),
        )
        hooks.event(idle(teacher))
        await waitFor(
          () => runtime.prompts.length === 2 + i,
          `nudge ${i + 1} was not sent (prompts=${runtime.prompts.length})`,
          10_000,
        )
        assert.equal(settled, false, `budget must not settle parent before exhaustion (at nudge ${i + 1})`)
      }

      const finalRoot = 'msg_ce_budget_final_root'
      const finalPrompt = runtime.prompts[runtime.prompts.length - 1]
      await acceptTeacherPrompt(hooks, runtime, teacher, finalPrompt, finalRoot)
      await hooks['experimental.chat.messages.transform'](
        { sessionID: teacher },
        {
          messages: [
            {
              info: { id: finalRoot, role: 'user', sessionID: teacher, agent: 'fast-teacher' },
              parts: [{ type: 'text', text: finalPrompt.body.parts[0].text }],
            },
          ],
        },
      )
      runtime.pushHostMessage(
        teacher,
        assistantMessage(
          teacher,
          'asst_ce_budget_final',
          finalRoot,
          'fast-teacher',
          'still not the fixed completion',
        ),
      )
      hooks.event(idle(teacher))

      const result = parseToml(await parentTool)
      assert.match(result.error, /budget exhausted/i)
      assert.equal(settled, true)
    })
  },
)

test('EXEC_027_cancel_after_Returned_before_Completion_fails_parent_at_second_await', async () => {
  await withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
    const student = 'ses_student_ce_second_await'
    await startStudent(hooks, student, 'msg_ce_sa_root', '第二 await 取消')

    const parentTool = hooks.tool.teacher.execute(
      { message: 'return 后取消' },
      toolContext(student, 'asst_ce_sa', 'call_ce_sa'),
    )
    await waitFor(() => runtime.prompts.length === 1, 'teacher prompt missing')
    const teacher = createdIds[0]
    await awaitPrompted(teacher)
    await acceptTeacherPrompt(hooks, runtime, teacher, runtime.prompts[0], 'msg_ce_sa_teacher_root')

    const returned = parseToml(
      await hooks.tool.return.execute(
        { message: '已落盘答案' },
        toolContext(teacher, 'asst_ce_sa_tool', 'call_ce_sa_return'),
      ),
    )
    assert.equal(returned.completion_text, 'Teacher answer returned to Student.')

    const gitDir = execFileSync('git', ['-C', directory, 'rev-parse', '--absolute-git-dir'], {
      encoding: 'utf8',
    }).trim()
    const qaSessionRoot = join(gitDir, 'wanxiangshu', 'student', student)
    assert.equal(existsSync(qaSessionRoot), true)

    hooks.event({ type: 'session.deleted', properties: { sessionID: student } })
    const result = parseToml(await parentTool)
    assert.match(result.error, /cancelled/)
    await waitFor(() => !existsSync(qaSessionRoot), 'QA cleanup after second-await cancel failed')
  })
})

test('EXEC_026_whitespace_normalized_fixed_completion_still_resolves_parent', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    const student = 'ses_student_ce_normalize'
    await startStudent(hooks, student, 'msg_ce_norm_root', 'normalize 校验')

    const parentTool = hooks.tool.teacher.execute(
      { message: 'normalize' },
      toolContext(student, 'asst_ce_norm', 'call_ce_norm'),
    )
    await waitFor(() => runtime.prompts.length === 1, 'teacher prompt missing')
    const teacher = createdIds[0]
    await awaitPrompted(teacher)
    await acceptTeacherPrompt(hooks, runtime, teacher, runtime.prompts[0], 'msg_ce_norm_teacher_root')

    await hooks.tool.return.execute(
      { message: '规范答案' },
      toolContext(teacher, 'asst_ce_norm_tool', 'call_ce_norm_return'),
    )

    const completionOutput = { text: 'provider trailing' }
    await hooks['experimental.text.complete'](
      { sessionID: teacher, messageID: 'asst_ce_norm_complete', partID: 'part_ce_norm' },
      completionOutput,
    )
    assert.equal(completionOutput.text, 'Teacher answer returned to Student.')

    runtime.pushHostMessage(
      teacher,
      assistantMessage(
        teacher,
        'asst_ce_norm_complete',
        'msg_ce_norm_teacher_root',
        'fast-teacher',
        `  ${completionOutput.text}  \n`,
      ),
    )
    hooks.event(idle(teacher))
    assert.equal(parseToml(await parentTool).answer, '规范答案')
  })
})
