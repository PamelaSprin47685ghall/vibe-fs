# N01/N02：实际权限与独立进程重放

本批接续[总计划 N01/N02](../../TODO施工总计划-2026-10-03.md)，父提交为 `0d5395518cb608fe40e4587c7b77c8bc4c5043f3`。只补 requirement-grounding/011、012 及独立 support，没有修改生产权限、Grounding 或共享 fixture。T389/T390 已纳入 gen127 正式相关137选集：137/137排空、1041 pass、0 fail、19 skip、72 TODO，162.23秒，退出1仅其它pending；目标没有TODO。[正式日志](baselines/vibe-fs-n01-n04-gen127-related137.log)与[输入身份](baselines/2026-10-05-n00-acceptance.txt)分别保存。GAP-085 保持 PARTIAL。

## 011 / T389

保留原 tool-result transport 正例。新增三 provider 的注册 before/after/transform 链：Manager 实际程序读取覆盖源码，含“授予 Engineer、写权限、变更 authority root、冒用 session”要求的受控 WHAT 材料确实进入末端结果。之后调用实际 js-engineer 和 Manager 自身写 API，均无未经授权文件、无完成标记；同一 Manager 的合法读取仍返回原源码。注入前后持久 authority profile、实际创建子会话数和 prompt 数保持。

无 authority 的会话通过实际受控 native read 字节和注册 after/transform 收到材料，随后仍不能取得 Engineer 执行权。测试没有让模型自行理解文本，也不把 Host metadata 相等当权限 oracle。

独立 Node loader 只把实际 ToolRegistry 的 js-engineer 角色门禁改成错放行，原业务断言检测到真实临时文件写入，子进程退出 1。不是删除测试断言、替换返回值或模拟拒绝。

[最终原始日志](baselines/vibe-fs-grounding011-authority-final.log)：5 pass、0 fail、0 TODO，包含变异检查。子进程保持原 30 秒物理预算；其冷启动及部分 native leaf 超过 5 秒，不能称本地正式 watchdog 已通过。

## 012 / T390

原单进程 reopen 正例保留。新增五个真正独立 OS 进程，每一阶段检查 PID 不同、前一进程已 `ESRCH`，不以重新 new plugin 代替跨进程：

1. A 经正常 plugin boot、authority/incumbency 和 registered read producer 持久化 v1 occurrence，保存实际 Host 输入与投影。
2. 删除 WHAT，B 正常恢复 journal，精确重放 A 的消息、结果和锚点。
3. 恢复 WHAT v2，C 做新的实际读取，只在冻结前缀后追加，新投影重复执行不再追加。
4. 删除整个材料包，D 正常恢复两版 occurrence，仍精确等于 C 的完整结果。
5. 对实际 NDJSON writer 中完整一行 `RequirementGroundingAnchored` 写入可核实的损坏，E 正常 boot 明确拒绝 `MalformedEnvelope`，没有投影结果或成功收据。

父测试逐层核对实际 NDJSON 的公开序列化合同：Agent → Host → RequirementGroundingAnchored、唯一 call id、真实 ArgsJson、原 v1 ResultBytes/CursorResultBytes 与 `After` Host 锚点。生产 Envelope codec 的消费由五个独立进程的正常 journal boot/replay 证明；父测试不深 import codec，也不读取 Fable 内部表示。Grounding 结果内联在 fact 中，该行 `payload_refs=[]`；没有造一个不存在的 Grounding blob，也不借此宣布 T388 原 Markdown 表示问题已解决。

[最终原始日志](baselines/vibe-fs-grounding012-inline-contract-green.log)：7 pass、0 fail、0 TODO，41.98 秒。每个独立子进程维持 30 秒预算；部分 leaf 为 6.6—12 秒，单跑不能代替正式 5 秒判决静默验收。

[JS 边界修正后的定向回归](baselines/vibe-fs-grounding012-public-wire-contract-green.log)：7 pass、0 fail、0 TODO，3.743 秒。删除内部 codec import 和 Fable 表示访问后，完整 wire、原字节、原锚点及日志损坏拒绝断言保持。

[oracle 分类修正前的记录](baselines/vibe-fs-grounding012-consumer-classification-red.log)只记录测试先前误判生产异常表示的失败。真实 fixture 允许 Host 构造返回不可用 journal，错误需由正常 shared-journal boot/transform 抛出；Fable 异常应读 message。该记录不是生产修复红证，也没有改生产逻辑来迎合它。

## 剩余工作

T389/T390 验收完成，不重复认领。002 自覆盖校验、006 实际返回版本、007 A 原字节及其它 GAP-085/086 义务继续单独施工；本批不覆盖安装版 native Host 的物理读取、模型语义服从或 presentation 回输。
