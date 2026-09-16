import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, 'resources/provider', rel), 'utf8')

const FIVE_OFFICES = [
  {
    id: 'coder-mutation',
    managerEn: /entrust mutation to a Coder/i,
    managerZh: /把 mutation 托付给 Coder/,
    forkEn: /Coder \/ Engineer[\s\S]{0,120}Changes repository source/,
    forkZh: /Coder \/ Engineer[\s\S]{0,80}改变 repository source/,
  },
  {
    id: 'inspector-existing-facts',
    managerEn: /entrust an Inspector/i,
    managerZh: /托付 Inspector/,
    forkEn: /Scout \/ Investigator[\s\S]{0,160}already exist in the repository/,
    forkZh: /Scout \/ Investigator[\s\S]{0,80}已经存在的事实/,
  },
  {
    id: 'devops-execution',
    managerEn: /entrust DevOps/i,
    managerZh: /托付 DevOps/,
    forkEn: /Technician \/ Operator[\s\S]{0,160}running world/,
    forkZh: /Technician \/ Operator[\s\S]{0,80}运行中的世界/,
  },
  {
    id: 'browser-external-provenance',
    managerEn: /entrust a Browser/i,
    managerZh: /托付 Browser/,
    forkEn: /Navigator \/ Researcher[\s\S]{0,160}external world with provenance/,
    forkZh: /Navigator \/ Researcher[\s\S]{0,80}外部世界的事实/,
  },
  {
    id: 'inquiry-reasoning',
    managerEn: /entrust Inquiry/i,
    managerZh: /托付 Inquiry/,
    forkEn: /Analyst \/ Inquirer[\s\S]{0,160}not yet clear/,
    forkZh: /Analyst \/ Inquirer[\s\S]{0,80}尚无明确答案/,
  },
]

test('WHAT[OFF-005] OFF_005_each_office_consequence_hits_manager_law_and_fork_description_in_both_locales', () => {
  const surfaces = {
    managerEn: read('role/manager/en.md'),
    managerZh: read('role/manager/zh-CN.md'),
    forkEn: read('tool/fork/description/en.md'),
    forkZh: read('tool/fork/description/zh-CN.md'),
  }
  for (const office of FIVE_OFFICES) {
    for (const key of ['managerEn', 'managerZh', 'forkEn', 'forkZh']) {
      assert.match(
        surfaces[key],
        office[key],
        `${office.id} must hit ${key} (projection drift → consequence lost)`,
      )
    }
  }
})
