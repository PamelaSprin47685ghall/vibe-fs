import assert from 'node:assert/strict'
import test from 'node:test'
import { record, sectionsOf, withReopenableJournal } from './support/record.mjs'

test('WHAT[work-record-008] actual Opening rendering preserves assignment bytes and existing requirement numbers', async () => {
  await withReopenableJournal(async (handle, reopen) => {
    const session = 'record-raw-opening'
    const assignment = '  Original task.\r\nKeep these bytes.  '
    const requirements = ['7. Keep the original identifier.', '42. 不重编号。']
    await record.captureOpening(handle, session, assignment, requirements)
    const expected = `Opening\n${assignment}\n${requirements.join('\n')}`
    assert.equal(await record.lifecycleWorkRecord(handle, session, true), expected)
    assert.equal(await record.lifecycleWorkRecord(await reopen(), session, true), expected)
  })
})

test('WHAT[work-record-008] actual committed Opening renders the original XTrace interval, not only the inline fields', async () => {
  await withReopenableJournal(async (handle, reopen) => {
    const session = 'record-opening-interval'
    // 内联字段与 trace 原字节故意不同：只有当区间材料真的取自 XTrace，
    // Opening 才能同时呈现两者（008：不从任务文本重建 Opening）。
    await record.captureOpening(handle, session, 'delegated charge', ['1. keep bytes'])
    await record.captureProjection(handle, session, {
      messages: [
        { role: 'user', parts: [
          { kind: 'text', text: 'raw first trace part' },
          { kind: 'text', text: 'second trace part of the same message' },
        ] },
        { role: 'assistant', parts: [{ kind: 'text', text: 'work after the charge' }] },
      ],
    })

    const rendered = await record.lifecycleWorkRecord(handle, session, true)
    const sections = sectionsOf(rendered)
    const opening = sections.Opening

    assert.ok(opening.includes('delegated charge'), rendered)
    assert.ok(opening.includes('1. keep bytes'), rendered)
    // trace owner 渲染的区间材料（role 前缀来自 XTrace.renderItem，非内联字段合成）。
    assert.ok(opening.includes('user: raw first trace part'), rendered)
    // 区间终点 = 首个语义 part 的 frontier（WorkRecordStart）：同消息的第二个 part 属工作材料。
    assert.equal(opening.includes('second trace part'), false, rendered)
    // 同一 part 不得同时被 Opening 与正文消费。
    assert.ok(sections['Recent work'].includes('user: second trace part of the same message'), rendered)
    assert.equal(sections['Recent work'].includes('raw first trace part'), false, rendered)
    // 冻结材料重开后逐字节相同。
    assert.equal(await record.lifecycleWorkRecord(await reopen(), session, true), rendered)
  })
})

test('WHAT[work-record-008] includeOpening=false omits the interval material instead of renaming it into the work body', async () => {
  await withReopenableJournal(async handle => {
    const session = 'record-opening-interval-omitted'
    await record.captureOpening(handle, session, 'delegated charge', [])
    await record.captureProjection(handle, session, {
      messages: [
        { role: 'user', parts: [{ kind: 'text', text: 'raw first trace part' }] },
        { role: 'assistant', parts: [{ kind: 'text', text: 'work after the charge' }] },
      ],
    })

    const rendered = await record.lifecycleWorkRecord(handle, session, false)
    // 子→父/冻结前缀方向省略 Opening：区间材料随之省略，其余工作正文不受影响。
    assert.equal(rendered.includes('delegated charge'), false, rendered)
    assert.equal(rendered.includes('raw first trace part'), false, rendered)
    assert.equal(sectionsOf(rendered)['Recent work'], 'assistant: work after the charge')
  })
})
