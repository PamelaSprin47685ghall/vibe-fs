# GAP-159：Predictor 身份与 office 准入

本卡约定的身份、Host 配置及 Fission 准入已修正。最终生产候选 680，包 SHA-256 `7d218a541e0a3df334eaa34e1bf7018ca08906062d07e6fed22c68caae886ff6`；库保持 `8bb1021 / 0.6.12`。正式相关回归 35 文件、327 通过、0 普通失败、0 skip、15 既存 TODO，文件全部完成、自有进程收尾确认通过。整组因 TODO 退出 1，不能写成整组验收通过；本卡只关闭计划明确列出的准入子式，不关闭完整 GAP-158。

## 原问题与实际后果

修复前 Predictor 名字解析成公开 Engineer 身份及 Persona，Host 又生成可见、允许 Fission 和源码工具的 Engineer 配置；另一处能力面却把 Predictor 当作无 office 权限，三者不一致。固定旧包 667 的实际 Plugin `chat.message(agent=predictor)` 确实能安装 Engineer authority，并路由 Engineer 模型。这不是单凭配置推测出来的问题。

本轮没有复现成功的 Predictor Fission 越权：旧版实际工具入口还会受到独立 origin 门禁拒绝。报告据此区分错误的身份安装、配置暴露和实际 Fission 后果，不把前两项冒称为已执行的越权工具。

## 最终职责与行为

- `ParticipantIdentity` 唯一拥有 Predictor 私有身份，公开 `Role` 为 None，稳定 Persona 为 Predictor；没有公开 `Role.Predictor`，也不再借 Engineer 身份。名字识别仍可认识内部名字，不因此开放 office。
- Host 的公开配置库存从现有角色目录筛选。Predictor 保留调用方模型配置，隐藏入口、清除旧 Engineer prompt，并拒绝全部工具；没有新增权限表或运行态身份缓存。
- 公开 authority 继续要求真实 office 身份。Predictor、未知名字不能从公共 chat 安装 authority；Manager、DevOps 及伪造 Engineer 的 Fission 请求拒绝后没有新 lane、prompt、abort 或其他 SDK 效果。夹具先真实创建父子容器，再核对效果增量，避免用没有父关系的请求假证准入。
- 合法 Manager 公开入口、真实 fork、Engineer 子参与者和 Fission 正控仍成立。真实只读 Replica 继承 owner 的 Role、SelectedAgent、Persona 和 provenance；Predictor 仍只提供模型池，不成为 Replica 的执行身份。
- 旧 authority v1 HumanRoot 必须证明 SelectedAgent 与 CanonicalRole 相符；正常 Engineer、Manager、DevOps 兼容，私有名字或字段矛盾明确拒绝。旧 AgentOwnerRoot 仍按原契约拒绝缺证升级。原件不改写。
- Journal 无法解码的事件不能证明安全切尾，新的 PlanCut 拒绝空 reset，保留该 EventStreamId 的故障。已持久化 cut 重放时，若当前故障指向另一个失败事件，则保持原故障；同事件 cut 与无当前故障的既有协议继续有效。判断仍归唯一 Integrator，没有第二份故障账本。

## 本轮引入的问题与修正

以下两项是本轮尚未提交候选引入的真实回退，不能归到原拆分，也不能归为测试误判。

1. **670：解码拒绝后被新 cut 清掉。** 收紧旧 Predictor 身份后，原无条件 `PlanCut {}` 会清掉该流故障，让 fresh Engineer 在原 run 未有 exact closure 时安装。旧 667 对同一请求拒绝。修正为必须能解释失败 Journal 事件，才可推断切尾；旧真实八行原件在新包拒绝新 root，健康 Engineer、Manager 流保留且可续接。
2. **673：旧的后续 cut 清掉较早故障。** 非作者用真实 667 producer 构造 Predictor root → HandleLinked → 冲突 HandleLinked → 生产自动 cut，producer 正常 SIGKILL。新版本较早在 Predictor root 解码失败，原重放却按 scope 用后一个事实的 cut 清掉它，允许新身份。678 起按确切 `FailedEventId` 仲裁；680 只提取同一私有判断以通过控制流静态门禁。原件双 Host、两次新 OS 冷读均保持阻塞。

反向正控也来自真实 667 producer：Engineer 先安装，竞争的 Predictor root 因原 active run 冲突而产生自己的 cut。该 cut 与新解码故障指向同一事件，仍允许重放，合法 Engineer 保留并可继续其 exact root。没有一律拒绝旧 cut 来掩盖回退。

### Journal 的有意收紧

无法解码的 Journal 载荷由过去的“bad+cut”改成 preparation 拒绝，无新事实、无 cut、无伪 fatal、无 revision 或 waiter 发布；writer 保留首次失败，后续调用不再 I/O。若 lock release 本身又失败，保留 `AppendNotAttempted(StoreRelease)` 与原 `AppendFailed`，不误报 `CommitUnknown`。

这只改变 Journal 的安全判定。Sphinx、Casebook 等规则仍按各自合同切尾，原矩阵不改预期。新增真实、可解码的 HandleLinked 语义冲突正控：bad+cut 同次持久追加，之后唯一原 fatal owner 杀 producer；丢 release 回执仍是 CommitUnknown，另一 OS 进程冷读保持原字节和最后合法投影。WHAT015/021/024 同步明确这一区分。

## 验收与证据

| 范围 | 最终结果与限度 |
|---|---|
| 35 个正式 requirement 文件 | 327 pass / 0 普通 fail / 15 TODO / 0 skip；35/35 文件完成，owned accepted；exit1 是保留待证条款，不是整组通过 |
| participant-identity009 | 五项正式用例通过；包括三份真实旧原件的拒绝/接受对照、exact closure 换身份与冷恢复 |
| durable-events024 | 21 项实际用例通过、2 个原有 TODO 保留；包含四格不可解码拒绝及两格真实 semantic cut/fatal/cold 正控 |
| 非作者实际 Host | 678 Node/Bun 公共入口各 3、原红回归各 1；六个 fresh OS 冷恢复场景，旧 Predictor 不被提升，健康 Manager 继续原 logicalRun；拒绝零 SDK 效果 |
| 非作者旧 cut | 667 原 producer、673 原红、678/680 双 Host 冷读及第二 OS 复验；同事件 cut 和健康 scope 为正控，原件逐字不改 |
| 类型与真实消费者 | 670 实际 F# 闭包编译一正三负；同一发射 JS 消费 678 Node/Bun，8 份 owner .fsi 同一，不重编译计新证明；原 EMR020 7 项和 Strength003 13 项，两 Host 各 20/20 |
| 产物 | 2076 个包文件逐项核对；680 相对 678 仅 IntegratorEngine.js 的私有函数提取，其他消费者与库字节未变；最终工作树另有同一性回执 |
| 构建与静态 | 175 注册面、839 模块通过；局部 format 通过；最终 check 退出 0，526 项既有 report-only。中间新增 match pyramid 曾被拒绝，已提取实际判断函数，无门禁豁免 |

完整原日志、版本、逐文件摘要、生产原件、红绿对照及复跑入口见 [evidence/GAP-159](evidence/GAP-159/README.md)。阶段运行不累加为最终项数。Bun 原生/消费者验证不冒称 CUT-00-R2 正式监督已完成。

## Review 与夹具误判

惰性 acquire 成功曾被误解为已重放；改为强制实际 snapshot 后证明流隔离。另有不存在的 identity006 文件、测试期待内部错误 case 名而非公开消息、未创建 events 目录的空历史观测、错误期待业务 preparation 返回 Rejected 而非 NotAttempted、合法新 RuntimeStarted 改变总计被误当身份变化等夹具错误。均保留原失败，修正前提后复验，没有迎合错误断言修改产品。

三份 NDJSON 被仓库通配忽略的问题由复审指出，随本提交逐项强制纳入；原件摘要与来源单列说明。文档曾把 Journal 的可解码前提误写成全规则前提，已纠正，未改变其他规则实现。

从暂存树导出独立 requirements/scripts，配合固定680发行包及隔离依赖消费009，五项全部通过、无 skip/TODO；没有借用工作区未跟踪的测试或原件。该结果是干净快照消费证明，不代替前述正式监督结果。

## 升级与剩余边界

本轮不迁移用户数据。切换前备份 `.git/wanxiangshu`，受影响的旧 Predictor 或矛盾身份流明确拒绝新身份；需要用有依据的 exact closure，或另行制定有输入清单的离线迁移，不能删账、猜成 Engineer 或重发未知命令。健康流不因此停止。

未重新认证任意人为构造的历史 cut；保留无当前故障及合法同事件 cut 的原协议。首次不可推断切尾仍会进入既有的一次全历史重放 fallback，不能解决本地 append WHAT017 在该异常路径上的成本承诺，此项另留性能边界。

完整 GAP-158、15 项既存 TODO、GAP-140、Trace 产品链、Bootstrap 与验证设施继续按计划推进。R12-U1 继续暂缓。随本提交正常推送 master；没有 registry 发布或 Host 部署。
