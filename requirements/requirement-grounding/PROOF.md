# PROOF — requirement-grounding

当前 package 已有正式合同与待实现契约测试，但 runtime 尚未落地。`test.todo` 是施工期 executable
spec，不计 active proof；因此 REQUIREMENT-SYSTEM-018 strict trace 应继续把本包报告为未证明，直到
对应 production semantic surface 落地并把 todo 转成 active test。

| 命题 | 当前测试落点 | 状态 / GAP | 目标 proof |
|---|---|---|---|
| REQUIREMENT-GROUNDING-001 | `tests/scope-resolution.test.mjs` | OPEN / GAP-017 | 临时 workspace discovery，不 hard-code 万象术包集 |
| REQUIREMENT-GROUNDING-002 | `tests/scope-resolution.test.mjs` | OPEN / GAP-017 | package self path 无 APPLIES-TO 仍命中；manifest 不能排除 self |
| REQUIREMENT-GROUNDING-003 | `tests/scope-resolution.test.mjs` | OPEN / GAP-017 | 正向 include + `!` exclude + 顺序 + comments + absent manifest |
| REQUIREMENT-GROUNDING-004 | `tests/scope-resolution.test.mjs` | OPEN / GAP-017 | overlap 返回全 package set + stable order |
| REQUIREMENT-GROUNDING-005 | `tests/grounding-delivery.test.mjs` | OPEN / GAP-018 | material 文件闭包、稳定排序与 digest；无 provider-visible bundle |
| REQUIREMENT-GROUNDING-006 | `tests/grounding-delivery.test.mjs` | OPEN / GAP-018 | 同 digest 一次；内容变更可重新 grounding；workspace 隔离 |
| REQUIREMENT-GROUNDING-007 | `tests/opencode-gate.test.mjs` | OPEN / GAP-019 | 自动 grounding 与普通 read wire 完全一致；首次 occurrence gap-anchored；同 gap 固定在 pair pseudo-skill 后；后续 byte-identical replay |
| REQUIREMENT-GROUNDING-008 | `tests/opencode-gate.test.mjs` | OPEN / GAP-019 | 首次 ungrounded mutation 零 effect；grounding 后新调用才执行 |
| REQUIREMENT-GROUNDING-009 | `tests/repository-programming-gate.test.mjs` | OPEN / GAP-020 | multi-file staged effect union；缺 grounding 全丢弃、零 partial commit/auto-rerun |
| REQUIREMENT-GROUNDING-010 | `tests/repository-programming-gate.test.mjs` | OPEN / GAP-020 | native/custom 同一 policy；换工具不能绕过 |
| REQUIREMENT-GROUNDING-011 | `tests/grounding-delivery.test.mjs` | OPEN / GAP-018 | 普通 read observation 不造 HumanRoot、不改 role/capability |
| REQUIREMENT-GROUNDING-012 | `tests/grounding-delivery.test.mjs` | OPEN / GAP-018 | typed anchored-read occurrence；retry/restart 原字节原位 replay；新 digest 只尾部追加；prefix law；internal loader 无递归 |

## GAP 事实源

| GAP | 缺口 | 关闭条件 |
|---|---|---|
| GAP-017 | scope catalog / APPLIES-TO matcher 尚不存在 production owner | `scope-resolution.test.mjs` 全部从 todo 转 active，命中正式 JS semantic surface，单跑绿 |
| GAP-018 | material/digest/durable anchored-read projection 与 authority-negative read projection 尚不存在 | `grounding-delivery.test.mjs` 全部 active；证明 restart 原位 replay、digest append-only、prefix law、dedupe/authority |
| GAP-019 | OpenCode native file observation/mutation 尚未经过 grounding gate，且尚无“自动 grounding ≡ 主动 read + 永久 gap projection”的 oracle | `opencode-gate.test.mjs` active；真实 transform canary 证明普通 read wire 等价、pair pseudo-skill 后固定落位、byte-identical replay + mutation zero-effect defer |
| GAP-020 | repository-programming 动态 multi-file effect set 尚未接 grounding，跨工具 no-bypass 无 oracle | `repository-programming-gate.test.mjs` active；transaction staging + native/custom equivalence 绿 |

## 运行

```text
node --test requirements/requirement-grounding/tests/*.test.mjs
node scripts/checks/requirement-trace.mjs --strict=requirement-grounding
```

第一条当前应表现为 todo-only、进程成功；第二条当前应失败并列出未 active proof，这正是 GAP-017..020
尚未关闭的机器表现。

