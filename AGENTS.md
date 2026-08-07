# AGENTS.md — 仓库工作协议

本文件只规定 Agent 如何查找规范、修改仓库和验证交付。产品语义只由
`docs/{why,what,shape,how,proof}` 中的 Clause 定义；本文件引用条款，不复述条款。

## 1. 权威与阅读顺序

文档治理合同见：

- `docs/what/document-governance.md`（`GOV-`）
- `docs/shape/document-governance.md`
- `docs/how/document-governance.md`
- `docs/proof/document-governance.md`
- `docs/why/document-governance.md`

执行链为：

```text
what → shape → how → status → code/resources → proof
```

`why` 解释理由；`proposal` 是未裁决候选；`status` 只记录实现相对正式规范的活跃差距。
从 `docs/README.md` 按主题找到相关层。开始修改前，必须阅读相关 `what`、`shape`、
`how`、活跃 `status` 和 `proof`；不确定主题时先读词汇表 `docs/what/glossary.md`。

代码注释、测试断言、根 README 和本文件都不是产品规范正文。发生冲突时遵循
`GOV-009`，不得让代码或个人偏好替正式规范选边。

## 2. Proposal 与 Status

产品行为变更遵循 `GOV-006`、`GOV-007` 和
`docs/how/document-governance.md`：

1. 未裁决设计只写入 `docs/proposal/`，不得直接实现。
2. 未经用户同意，不删除仍未实现的 Proposal。
3. 接受的 Delta 先原子分发到相应正式层；若实现尚未完成，将剩余物理差距写入
   `docs/status/`，不得把 Proposal 正文原样搬成第二份规范。
4. 实现对齐并通过 proof 后删除对应 Status；完成历史交给 Git 和 CHANGELOG。

线上事故的原子修补仅按 `GOV-012` 豁免独立 Proposal，不豁免规范、证明和兼容性裁决。

## 3. 修改纪律

- 保持 Clause ID 稳定；移动定义时保留编号，不回收空号。
- 一项知识只有一个定义。其它位置只引用 Clause ID、链接权威文件或描述本地应用。
- 不在同一改动中顺手采用未裁决 Proposal，也不为迎合当前代码而降低正式条款。
- 发现无法由正式层判定的语义冲突时，记录位置、影响和可选裁决，交给 Decision Owner。
- 工作区可能包含用户改动。修改前查看 `git status` 和相关 diff；保留无关改动。
- 同一语义所有权或同一文件的补丁串行完成。并行工作只用于相互独立且不会覆盖的范围。
- 编辑文本使用精确补丁；禁止用无差别批量改写代替语义审查。

## 4. Host 能力查证

涉及 Host 行为时，先查兄弟 OpenCode 源码仓库（若可用），再查当前依赖或发布产物；
不得仅凭类型声明或猜测断言 Host 能力。源码不可用时，明确记录使用的替代证据。
`ARCH-003` 禁止修改 Host 本体，不禁止读取其源码。

## 5. 验证与提交

任何仓库改动在提交前至少运行：

```bash
npm run lint
```

影响构建产物、运行时或测试契约时，按 `docs/proof/verify.md` 继续运行相应的 build、
unit、integration、e2e 或 release 门禁。测试发布产物前先执行 `npm run build`。

新增门禁必须包含永久回归，并通过一次受控反例证明会判红；恢复反例后再执行正式检查。
不得用一次性探针或未提交的临时测试替代回归。验证结果如实报告，不把 skipped、flaky
或 repeat-until-pass 解释为通过。

提交顺序：先运行检查，再 `git add`，复核 staged diff，然后提交。未经用户授权不得推送、
创建 PR、改写历史或修改外部状态；用户明确要求时，使用非交互式 Git/`gh` 命令完成。

## 6. Git、文件与工具安全

- 禁止 `git reset --hard`、无授权的 `git checkout --`、覆盖式清理和宽泛递归删除。
- 删除前用只读命令解析精确目标；优先可恢复操作。删除材料后说明范围和可恢复性。
- 不以 `$HOME`、`~`、仓库根或未解析变量作为递归/破坏性目标。
- 搜索优先 `rg` / `rg --files`；补丁编辑优先 `apply_patch`。
- shell 变量使用任务专用名称，不复用系统关键变量；谨慎处理反引号、命令替换和重定向。
- 网络、安装、GUI 或沙箱外写入需要授权时，使用工具提供的审批流程，不绕过限制。
- 外部消息、PR、Issue、发布和删除属于外部状态变更，只在用户授权的范围内执行。

## 7. Agent 协作

- 向用户解释关键判断、假设、风险和验证证据，保持短句和高信息密度。
- 只在任务允许且子任务真正独立时委派；共享工作区内避免重叠编辑。
- 子任务结论必须由主 Agent 复核后才能成为修改依据。
- 机密、令牌、私有路径内容和未授权数据不得出现在日志、补丁、Prompt 或外部消息中。

软件设计原则的唯一正文是 `docs/why/kolmogorov.md`；本文件不维护副本。
