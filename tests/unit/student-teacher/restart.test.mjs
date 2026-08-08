import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'

import { awaitPrompted, withRestartablePlugin } from '../plugin/plugin-fixture.mjs'

const waitFor = async (predicate, message) => {
  const deadline = Date.now() + 2000
  while (!predicate()) {
    if (Date.now() >= deadline) throw new Error(message)
    await new Promise((resolve) => setImmediate(resolve))
  }
}

const promptKeyOf = (prompt) => prompt.body.metadata.wanxiangshu_prompt_key

const context = (sessionID, messageID, callID) => ({
  sessionID,
  messageID,
  callID,
  abort: new AbortController().signal,
})

const acceptTeacherPrompt = async (hooks, host, prompt, teacher, ordinal) => {
  const messageID = `msg_teacher_restart_${ordinal}`
  const metadata = { wanxiangshu_prompt_key: promptKeyOf(prompt) }
  await hooks['chat.message'](
    { sessionID: teacher, agent: 'fast-teacher', messageID },
    {
      message: { id: messageID, role: 'user', sessionID: teacher, agent: 'fast-teacher' },
      parts: [{ type: 'text', text: prompt.body.parts[0].text, metadata }],
    },
  )
  host.pushHostMessage(teacher, {
    info: { id: messageID, role: 'user', sessionID: teacher, agent: 'fast-teacher', metadata },
    parts: [{ type: 'text', text: prompt.body.parts[0].text, metadata }],
  })
}

const completeTeacherReturn = async (hooks, host, teacher, ordinal) => {
  const messageID = `asst_teacher_restart_completion_${ordinal}`
  const output = { text: 'provider trailing prose' }
  await hooks['experimental.text.complete'](
    { sessionID: teacher, messageID, partID: `part_teacher_restart_completion_${ordinal}` },
    output,
  )
  assert.equal(output.text, 'Teacher answer returned to Student.')
  host.pushHostMessage(teacher, {
    info: {
      id: messageID,
      role: 'assistant',
      sessionID: teacher,
      parentID: `msg_teacher_restart_${ordinal}`,
      agent: 'fast-teacher',
      finish: 'stop',
      time: { completed: Date.now() },
    },
    parts: [{ type: 'text', text: output.text }],
  })
  hooks.event({
    type: 'session.status',
    properties: { sessionID: teacher, status: { type: 'idle' } },
  })
}

test('HOST_014_plugin_restart_rebuilds_Student_control_and_reuses_proven_Teacher', async () => {
  await withRestartablePlugin(async (start, directory, host) => {
    const student = 'ses_student_restart'
    const rootID = 'msg_student_restart_root'
    const rootText = '请在重启前后持续学习。'

    const first = await start()
    await first['chat.message'](
      { sessionID: student, agent: 'fast-student', messageID: rootID },
      {
        message: { id: rootID, role: 'user', sessionID: student, agent: 'fast-student' },
        parts: [{ type: 'text', text: rootText }],
      },
    )

    const firstTool = first.tool.teacher.execute(
      { message: '重启前问题' },
      context(student, 'asst_student_restart_1', 'call_teacher_restart_1'),
    )
    await waitFor(() => host.prompts.length === 1, 'first Teacher prompt was not sent')
    const teacher = host.createdIds[0]
    await awaitPrompted(teacher)
    await acceptTeacherPrompt(first, host, host.prompts[0], teacher, 1)
    const abortsBeforeFirstReturn = host.abortedIds.length
    const firstReturn = await first.tool.return.execute(
      { message: '重启前回答' },
      context(teacher, 'asst_teacher_restart_1', 'call_return_restart_1'),
    )
    assert.equal(parseToml(firstReturn).completion_text, 'Teacher answer returned to Student.')
    await completeTeacherReturn(first, host, teacher, 1)
    assert.equal(parseToml(await firstTool).answer, '重启前回答')
    assert.equal(host.abortedIds.length, abortsBeforeFirstReturn)
    await first.dispose()

    const second = await start()
    // OpenCode may replay the accepted HumanRoot when rebuilding a provider
    // request. The same physical identity must reconstruct control without
    // appending the root bytes twice.
    await second['chat.message'](
      { sessionID: student, agent: 'fast-student', messageID: rootID },
      {
        message: { id: rootID, role: 'user', sessionID: student, agent: 'fast-student' },
        parts: [{ type: 'text', text: rootText }],
      },
    )

    const secondTool = second.tool.teacher.execute(
      { message: '重启后问题' },
      context(student, 'asst_student_restart_2', 'call_teacher_restart_2'),
    )
    await waitFor(() => host.prompts.length === 2, 'recovered Teacher prompt was not sent')
    assert.equal(host.createdIds.length, 1, 'restart must reuse the one proven Host child')
    assert.equal(host.prompts[1].path.id, teacher)
    await acceptTeacherPrompt(second, host, host.prompts[1], teacher, 2)
    const abortsBeforeSecondReturn = host.abortedIds.length
    const secondReturn = await second.tool.return.execute(
      { message: '重启后回答' },
      context(teacher, 'asst_teacher_restart_2', 'call_return_restart_2'),
    )
    assert.equal(parseToml(secondReturn).completion_text, 'Teacher answer returned to Student.')
    await completeTeacherReturn(second, host, teacher, 2)
    assert.equal(parseToml(await secondTool).answer, '重启后回答')
    assert.equal(host.abortedIds.length, abortsBeforeSecondReturn)

    const gitDir = execFileSync('git', ['-C', directory, 'rev-parse', '--absolute-git-dir'], {
      encoding: 'utf8',
    }).trim()
    const runRoot = join(gitDir, 'wanxiangshu', 'student', student)
    const [logicalRun] = readdirSync(runRoot)
    assert.equal(
      readFileSync(join(runRoot, logicalRun, 'QA.md'), 'utf8'),
      [rootText, '重启前问题', '重启前回答', '重启后问题', '重启后回答'].join('\n\n'),
    )
    await second.dispose()
  })
})
