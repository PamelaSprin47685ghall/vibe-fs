# obligation-ledger 测试证明范围

新上游把本包收窄为 7 条 Host 待办 UI 投影规则；认知提交归 cognitive-workspace，不能把旧 28 条账本协议搬回。本目录按新编号测试。

- `001`：真实 AssumeAdmission 的状态、必填项、默认 priority、旧顶层规划参数正反例；TodoSinkSurface 只把真实 parser 的结果交给真实 TodoSink，不自行补值或决定准入。
- `002`：真实 sink 保留重复行、顺序、中文、多行与空清单。它只证明输出材料，未证明安装版 Host 已写入。
- `003–006`：desired/applied、旧 owner 迟到、UI 漂移、同步失败与无裁决权保留 TODO。当前 TodoSink 没有生产调用者，AssumeTool 只提交/返回画板；不得把新接缝存在称为 UI 管道已接通。
- `007`：现有 legacy 接口明确拒绝并不返回可用事实；受控审计读取尚未实现。拒绝旧调用不等于已满足历史读取合同。

旧施工迁移输入共 19 份原样保留在 [historical-tests](../historical-tests/)，不进入活动测试发现集；`007` 的原合并标记由无损 gzip 保留，其余为 `.mjs.txt`。解压后逐文件 SHA-256 与归档前一致，见 [SHA256SUMS](../historical-tests/SHA256SUMS)。完整旧版本位于备份提交 `1d7098a38f8419fa8586a6f695af61a18125f959`。

| 旧规则/证据 | 新归属或处置 |
|---|---|
| 001–006、013、016、017、023、027 的交付债务、焦点、规划承诺和细化协议 | 旧协议退出；新 todos 输入/显示只按本包001/002；自由画板与阶段归 cognitive-workspace，不恢复 T1/BlindPlan 门禁。 |
| 007–012、018、025、026 的顺序、重放、提交与恢复 | cognitive-workspace-003—006/009；已有该包局部测试不等于全部故障窗口已闭合。 |
| 003、015、024 的 UI 兼容 | 本包001—005；保留真实 parser/sink 正反例，Host交付仍TODO。 |
| 014、022 的清单不裁决质量/退休 | 本包006与relay-assessment/relay-retirement；实际入口组合仍TODO。 |
| 019 的 Life 隔离、021 的 lag-1 | 已被 session 画板跨Life保留与新阶段窗口替代；历史事件只读见007，不能激活旧规则。 |
| 028 的真实 fatal 结算 | execution-failure-policy 与 cognitive-workspace 的故障边界；不沿用旧MagicTodo切点或伪造fatal描述符。 |

局部命令：`node --test requirements/obligation-ledger/tests/*.test.mjs`。generation3统一构建及正式选集已执行本包parser/sink，UI生产交付仍TODO。003文字补回后的最终复验见[兼容验收记录](../../../proposals/20模块兼容修复与验收-2026-09-28.md)，TODO不算通过。
