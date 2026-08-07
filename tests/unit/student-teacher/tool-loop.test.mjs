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

      while (runtime.prompts.length <= index) await new Promise((resolve) => setImmediate(resolve))
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

      const teacherReturn = await hooks.tool.return.execute(
        { message: answer },
        toolContext(teacher, `asst_teacher_${index}`, `call_return_${index}`),
      )
      assert.equal(teacherReturn, 'OK')

      const delivered = parseToml(await studentRun)
      assert.equal(delivered.answer, answer)
      assert.equal(createdIds.length, 1, 'all calls must reuse exactly one Teacher Session')
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
