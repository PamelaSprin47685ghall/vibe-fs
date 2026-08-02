# 休克—退火迁移机器证据

本目录是 `docs/archive/shock-anneal-2026/` 的原始机器证据，全部来自 `STATUS/evidence/`，
经 `git mv` 原样迁移，未修改、未复制、未重新生成。

## 采集链

| 目录/文件 | 性质 | 绑定 |
|-----------|------|------|
| `pre-shock/` | 旧世界最后一次完整机器反馈（迁移前基线） | commit 见 `pre-shock/COMMIT.txt` |
| `post-freeze/` | 封炉工装自证：静态检查器能跑、能测出预期残留 | 基线 `274a30aa` |
| `post-anneal2/` | 退火二完成：第 0–2 层反馈恢复，验证层换语言 | `2b30301c`，TZ=CST |
| `host-context-recovery.md` | Host compaction 源码证据（HOST-006 第 6–10 项） | HOST-006 |
| `host-transform-run-binding.md` | Host assistant message = provider request = attempt | HOST-010 |
| `k11-mutation-red-green.md` | K11 变异红绿记录 | 包 K |
| `manager-worktree-durable-ownership.md` | ORCH-006 durable worktree 修复 | `9fcaad24` |
| `orchestrator-restart-recovery-fixes.md` | orchestrator-restart-publish 修复链 | 退火三 |

## 使用说明

- 本目录是历史档案，不随当前代码更新。
- 结论性总结见 `../FINAL-REPORT.md` §13、§17；本目录保留原始输出以便审计。
- 修改或复制输出会削弱证据价值；如需新证据，请放入 `docs/evidence/` 而不是此处。
