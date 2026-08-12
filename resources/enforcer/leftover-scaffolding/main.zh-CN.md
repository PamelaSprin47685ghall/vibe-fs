# leftover-scaffolding — Main 中文版

## 现在该做什么
为每个 transitional artifact 做二选一：删除，或正式晋升。晋升意味着它获得清晰 purpose、owner、tests、docs、entry point 与 maintenance promise；不能只因为“已经在 repo 里”就继续存在。

## 为什么这很重要
临时 artifact 越老越难删，因为后来的人会把“它活了这么久”误当成“它一定重要”。于是 accidental survival 变成伪需求，repository 不断积累无人敢碰的神秘脚手架。

## 常见假修复
- 移到 `scripts/old` / `misc`。
- 加 `temporary` comment，却不设 exit。
- 保留 rollout flag “以后紧急时可能用”。
- 把所有 scaffolding 一律产品化，制造更多长期工具。

## 验证
任务/迁移结束后，搜索其临时 marker、flag、script、probe。剩下的每一个都应能出示长期维护合同，否则删除。

## 完成条件
过渡结束时，支架也结束；留下的东西都是被有意维护的产品/工具，而不是历史事故。
