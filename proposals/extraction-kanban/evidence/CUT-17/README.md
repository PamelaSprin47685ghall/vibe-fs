# CUT-17 证据

返回 [卡片](../../cards/CUT-17.md)。全部在共享机 `wt/CUT-06-main` 上实际运行；未跑全量 `scripts/verify.mjs` 与集成 `run.mjs`（共享机内存紧，按 PROTOCOL 只跑相关测试）。

## 子范围①：Plan 任期过滤（wanxiangli/projection `tenure`，0.27.0 发布，接线用 0.28.0）

- base = master `be5330991`（gitlink `ece7176` / 0.26.0）；candidate = 本提交（gitlink `351e3c5` / 0.28.0，`TenureIsolation.assemble` 改由库裁决）。
- 测试集：`tenure-suite.files`（participant-horizon 001–015、semantic-trace 002、work 001–050，共 66 文件 242 用例）。
- 结果：base 与 candidate 均为 221 pass / 9 fail / 1 skipped / 11 todo，224 条 verdict **逐条完全相同**（`tenure-suite.verdicts`，`diff` 空）。9 个失败（work 009/011/019×3/020/021/024/026）在 base 上同样失败，为既有问题，与本刀无关。
- 产物：`dist/Mission/Planning/TenureIsolation.js` 第 4 行 `import { Projection } from "wanxiangli/projection"`。
- 双算（`tenure-diff.mjs`）：经公开面 `PlanningSurface.assembleTenureMessages`，旧实现取 `wt/CUT-08-main/dist`（其 `TenureIsolation.fs` 与 master 本刀前同源，`git log` 核对），新实现取本工作树 dist；3 个种子 × 3000 轮随机 Host 消息（含无 id、合成、空 parts、未知角色、游标缺失）共 9000 次比较 **0 分歧**，其中约 2000 次双方都以 “no stable XTrace message identity” 拒绝。
- 负控：把库 `keepsInteraction` 的 `cursor >= opening` 改成 `>`，同一双算 3000 轮抓到 197 次分歧（`tenure-diff.txt` 末行）；恢复后 0 分歧。第一次负控未抓到是因为最初的 LCG 低位相关、从未生成“游标恰等于开场”的样本——已换成 mulberry32 后重跑，上面数字均为换后结果。

### 子范围① 变基复核（eng-cut17c-goose，落地前）

- 原提交 `1788e8f41` 中断未推；变基到 master `0ba124c4a`（其间落地 CUT-09、CUT-22、任务书A 文档等）后重建。
- base = master `0ba124c4a` 全新构建（`wt/CUT-17-base`）；candidate = 变基后本提交全新构建。同一 66 文件：两边均 223 pass / 8 fail / 1 skipped / 11 todo，225 条 verdict **逐条相同**（`tenure-suite.verdicts` 已按此次 candidate 重写）。与首次记录相比多出一条 work-020 新用例、work-020 旧失败转绿，两边同样如此，来自 master 其间的提交，不是本刀。
- 双算 `tenure-diff.mjs`（旧实现改指 `wt/CUT-17-base/dist`）3 种子 × 3000 = 9000 次 **0 分歧**，与首次数字一致。
- 再次变基到 master `03295f1fd`（落地 CUT-10 路由/许可/容量与 work 测试迁移，gitlink → `d2ce694`/0.35.0）后重建 candidate：同一 66 文件 233 pass / 2 fail / 1 skipped / 11 todo；2 个失败均在上面 base 的 8 个失败之内（其余 6 个由 master 其间的测试迁移转绿），无新增失败；双算 9000 次仍 0 分歧。本刀不改 gitlink。
