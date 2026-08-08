import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { existsSync, mkdirSync, readdirSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'

import { awaitPrompted, withExecutablePlugin } from '../plugin/plugin-fixture.mjs'

const waitFor = async (predicate, message) => {
  const deadline = Date.now() + 2000
  while (!predicate()) {
    if (Date.now() >= deadline) throw new Error(message)
    await new Promise((resolve) => setImmediate(resolve))
  }
}

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

const toolContext = (sessionID, messageID, callID) => ({
  sessionID,
  messageID,
  callID,
  abort: new AbortController().signal,
})

test('EXEC_025_learning_idle_switches_to_compile_and_final_return_deletes_QA_before_text_completion', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    const student = 'ses_student_compile'
    const rootID = 'msg_student_compile_root'
    const rootText = '学习并生成一个可迁移技能。'

    await hooks['chat.message'](
      { sessionID: student, agent: 'fast-student', messageID: rootID },
      {
        message: { id: rootID, role: 'user', sessionID: student, agent: 'fast-student' },
        parts: [{ type: 'text', text: rootText }],
      },
    )
    runtime.pushHostMessage(student, userMessage(student, rootID, 'fast-student', rootText))
    runtime.pushHostMessage(
      student,
      assistantMessage(student, 'asst_student_learn', rootID, 'fast-student', '学习阶段已自然结束。'),
    )

    hooks.event(idle(student))
    await waitFor(() => runtime.prompts.length === 1, 'StudentCompile continuation was not sent')

    const compilePrompt = runtime.prompts[0]
    assert.equal(compilePrompt.path.id, student)
    assert.equal(compilePrompt.body.agent, 'fast-student')
    assert.equal(compilePrompt.body.model, undefined)
    assert.equal(compilePrompt.body.tools.teacher, false)
    for (const allowed of ['read', 'glob', 'grep', 'write', 'edit', 'return']) {
      assert.equal(compilePrompt.body.tools[allowed], true, `${allowed} must be enabled in StudentCompile`)
    }

    const gitDir = execFileSync('git', ['-C', directory, 'rev-parse', '--absolute-git-dir'], {
      encoding: 'utf8',
    }).trim()
    const sessionQaRoot = join(gitDir, 'wanxiangshu', 'student', student)
    const [logicalRun] = readdirSync(sessionQaRoot)
    const qaPath = join(sessionQaRoot, logicalRun, 'QA.md')
    assert.equal(existsSync(qaPath), true, 'QA must still exist throughout StudentCompile')

    const compileHostMessage = runtime.messages.at(-1)
    await hooks['chat.message'](
      {
        sessionID: student,
        agent: 'fast-student',
        messageID: compileHostMessage.id,
      },
      {
        message: {
          id: compileHostMessage.id,
          role: 'user',
          sessionID: student,
          agent: 'fast-student',
        },
        parts: compileHostMessage.parts,
      },
    )

    await assert.rejects(
      async () =>
        hooks['tool.execute.before'](
          { sessionID: student, tool: 'write', callID: 'call_flat_skill' },
          { args: { filePath: '.agent/skills/example.md' } },
        ),
      /exactly \.agent\/skills\/<skill-name>\/SKILL\.md/,
    )

    const skillRelativePath = '.agent/skills/example/SKILL.md'
    const skillPath = join(directory, skillRelativePath)
    await hooks['tool.execute.before'](
      { sessionID: student, tool: 'write', callID: 'call_valid_skill' },
      { args: { filePath: skillRelativePath } },
    )
    mkdirSync(join(directory, '.agent', 'skills', 'example'), { recursive: true })
    writeFileSync(skillPath, '# Missing frontmatter\n')

    const invalidReturn = parseToml(
      await hooks.tool.return.execute(
        { message: '不应完成。' },
        toolContext(student, 'asst_student_compile_invalid', 'call_student_return_invalid'),
      ),
    )
    assert.match(invalidReturn.error, /YAML frontmatter/)
    assert.equal(existsSync(qaPath), true, 'invalid SKILL must keep QA for repair')

    writeFileSync(
      skillPath,
      '---\nname: example\ndescription: Preserve one proven causal chain.\n---\n\n# Example\n\nPreserve one proven causal chain.\n',
    )

    const finalMessage = '已生成并检查 .agent/skills/example/SKILL.md；重启 OpenCode 后加载。'
    const returnResult = parseToml(
      await hooks.tool.return.execute(
        { message: finalMessage },
        toolContext(student, 'asst_student_compile', 'call_student_return'),
      ),
    )
    assert.equal(returnResult.final_message, finalMessage)
    assert.equal(existsSync(qaPath), false, 'return must delete QA before yielding its tool result')

    const textOutput = { text: 'provider paraphrase that must not escape' }
    await hooks['experimental.text.complete'](
      { sessionID: student, messageID: 'asst_student_final_after_return', partID: 'part-final' },
      textOutput,
    )
    assert.equal(textOutput.text, finalMessage)

    runtime.pushHostMessage(
      student,
      assistantMessage(student, 'asst_student_compile', compileHostMessage.id, 'fast-student', finalMessage),
    )
    hooks.event(idle(student))
  })
})

test('EXEC_025_Teacher_plain_text_only_nudges_and_does_not_complete_the_parent_tool', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    const student = 'ses_student_teacher_idle'
    const rootID = 'msg_student_teacher_idle_root'

    await hooks['chat.message'](
      { sessionID: student, agent: 'fast-student', messageID: rootID },
      {
        message: { id: rootID, role: 'user', sessionID: student, agent: 'fast-student' },
        parts: [{ type: 'text', text: '学习一个问题。' }],
      },
    )

    let settled = false
    const parentTool = hooks.tool.teacher
      .execute(
        { message: '请调查后回答。' },
        toolContext(student, 'asst_student_waiting', 'call_teacher_idle'),
      )
      .finally(() => {
        settled = true
      })

    await waitFor(() => runtime.prompts.length === 1, 'initial Teacher prompt was not sent')
    const teacher = createdIds[0]
    await awaitPrompted(teacher)
    const firstPrompt = runtime.prompts[0]
    const promptKey = firstPrompt.body.metadata.wanxiangshu_prompt_key
    const teacherRootID = 'msg_teacher_idle_root'

    await hooks['chat.message'](
      { sessionID: teacher, agent: 'fast-teacher', messageID: teacherRootID },
      {
        message: { id: teacherRootID, role: 'user', sessionID: teacher, agent: 'fast-teacher' },
        parts: [
          {
            type: 'text',
            text: firstPrompt.body.parts[0].text,
            metadata: { wanxiangshu_prompt_key: promptKey },
          },
        ],
      },
    )
    runtime.pushHostMessage(
      teacher,
      userMessage(
        teacher,
        teacherRootID,
        'fast-teacher',
        firstPrompt.body.parts[0].text,
        { wanxiangshu_prompt_key: promptKey },
      ),
    )
    runtime.pushHostMessage(
      teacher,
      assistantMessage(teacher, 'asst_teacher_plain', teacherRootID, 'fast-teacher', '普通正文不能成为答案。'),
    )

    hooks.event(idle(teacher))
    await waitFor(() => runtime.prompts.length === 2, 'Teacher idle nudge was not sent')
    assert.equal(runtime.prompts[1].path.id, teacher)
    assert.equal(settled, false, 'ordinary Teacher text must not settle the parent teacher tool')

    const returned = await hooks.tool.return.execute(
      { message: '只有 return 的文本才是答案。' },
      toolContext(teacher, 'asst_teacher_plain', 'call_teacher_return_after_idle'),
    )
    assert.equal(parseToml(returned).completion_text, 'Teacher answer returned to Student.')

    const toolRunOutput = { text: 'text belonging to the tool-calling assistant' }
    await hooks['experimental.text.complete'](
      { sessionID: teacher, messageID: 'asst_teacher_plain', partID: 'part_teacher_tool_run' },
      toolRunOutput,
    )
    assert.equal(
      toolRunOutput.text,
      'text belonging to the tool-calling assistant',
      'the return-calling provider run cannot impersonate the following terminal completion',
    )

    const completionOutput = { text: 'provider trailing prose' }
    await hooks['experimental.text.complete'](
      { sessionID: teacher, messageID: 'asst_teacher_return_complete', partID: 'part_teacher_complete' },
      completionOutput,
    )
    assert.equal(completionOutput.text, 'Teacher answer returned to Student.')
    runtime.pushHostMessage(
      teacher,
      assistantMessage(
        teacher,
        'asst_teacher_return_complete',
        teacherRootID,
        'fast-teacher',
        completionOutput.text,
      ),
    )
    hooks.event(idle(teacher))

    assert.equal(parseToml(await parentTool).answer, '只有 return 的文本才是答案。')
    assert.deepEqual(runtime.abortedIds, [], 'successful Teacher return must not abort its turn')
  })
})

test('EXEC_025_session_delete_releases_teacher_waiter_aborts_satellite_and_removes_QA', async () => {
  await withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
    const student = 'ses_student_cancel'
    const rootID = 'msg_student_cancel_root'

    await hooks['chat.message'](
      { sessionID: student, agent: 'fast-student', messageID: rootID },
      {
        message: { id: rootID, role: 'user', sessionID: student, agent: 'fast-student' },
        parts: [{ type: 'text', text: '开始后取消。' }],
      },
    )

    const parentTool = hooks.tool.teacher.execute(
      { message: '这个问题将被取消。' },
      toolContext(student, 'asst_student_cancel', 'call_teacher_cancel'),
    )
    await waitFor(() => createdIds.length === 1, 'Teacher satellite was not created')

    const gitDir = execFileSync('git', ['-C', directory, 'rev-parse', '--absolute-git-dir'], {
      encoding: 'utf8',
    }).trim()
    const qaSessionRoot = join(gitDir, 'wanxiangshu', 'student', student)
    assert.equal(existsSync(qaSessionRoot), true)

    hooks.event({ type: 'session.deleted', properties: { sessionID: student } })
    await waitFor(
      () => runtime.abortedIds.includes(createdIds[0]) && !existsSync(qaSessionRoot),
      'Student cancellation did not finish private cleanup',
    )

    const result = parseToml(await parentTool)
    assert.match(result.error, /cancelled/)
    assert.deepEqual(runtime.abortedIds, [createdIds[0]])
  })
})
