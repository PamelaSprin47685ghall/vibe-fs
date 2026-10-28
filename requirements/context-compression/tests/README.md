# context-compression 测试导航

本包规格在 `WHAT.md`，本文只说明每个测试文件观察哪些条款，便于定位改动影响范围；不补充验收规则，不重复条款正文。

## 本目录测试

| 文件 | 观察的条款 | 观察内容 |
|---|---|---|
| `001.test.mjs` | 001 | 真实 OpenCode hooks 共用一个 `ProviderCalibration` 归属，会话删除时清除；`chat.params` 只记录 provider 声明的 context limit 与实际用量，不作容量推导。 |
| `002.test.mjs` | 002 | `CompactionPolicySurface` 的三个必设键与取值；设置写不进时启动失败并给出理由，系统 compaction 观察本身不再拒绝启动；遏制按折叠谓词而非原始字段判定；待重锚的是最新未处理的 compaction。 |
| `004.test.mjs` | 004 | 终态文本判定：空文本与纯 XML 被拒且拒绝原因可区分（`Empty` / `XmlOnly`），含正文的散文被接受，`isValid` 与 `check` 一致。 |
| `005.test.mjs` | 005 | 一次确认失败推进一格预算；谈论溢出的完成文本仍是有效散文，控制流不按错误字符串分类。 |
| `006.test.mjs` | 006 | 角色在重试与连续失败间固定；连续失败耗尽预算后停止自动重试。 |
| `007.test.mjs` | 007 | attempt 结局按 `ProviderRequestKind` 分派，`WorkMain` 与 `InteractionRepair` 后继不同。 |
| `008.test.mjs` | 008 | 工作会话使用普通主请求种类；宿主 compaction 推进 wire epoch 但不重置 ScopeProgress。 |
| `013.test.mjs` | 013 | 诊断 emit 非致命，不写裸 console 载荷。 |
| `017.test.mjs` | 017 | `suppressHostMessagesByIds` 只删指定项，真人 user 消息保持同一 Host 对象引用；无 id／无归属消息保留。 |
| `019.test.mjs` | 019 | 重锚 retire 旧 pair wire 而保留历史，允许新 pair；游标重锚先剥旧后缀再加新段；未知 fact case 响亮拒绝。 |
| `023.test.mjs` | 023 | retry 由失败预算驱动，与 wall clock 无关。 |
| `030.test.mjs` | 030 | 被覆盖的 `assume` 调用与配对结果原样存活于裁剪。 |
| `034.test.mjs` | 033、034 | 历次 resetScope 调用与同消息结果保留；旧 OMP 工具结果被清、operator 与新工具保留；reset 后的新 operator 消息不改 Scope 也不带倒计时尾巴；中立计划不产生缺失估计 steer；agent-attributed 自定义消息被清而 user-attributed 存活。 |
| `038.test.mjs` | 038 | HNI 中立 Scope 视图只决定保留与裁剪，不发明 steer 或后缀；OMP 独立 toolResult 走同一计划。 |
| `039.test.mjs` | 039 | OpenCode 请求副本原地更新并索取首次估计，Host 历史不变；reset 后只保留该调用与其之后的工作，无倒计时后缀。 |
| `040.test.mjs` | 040 | `SnapshotScope` 保留与较早 reset 调用同消息的结果，以及已完成的 reset 状态并删除无关旧工作。 |
| `041.test.mjs` | 041 | `session.deleted` 只释放自己的 scope actor。 |
| `043.test.mjs` | 043 | `OutboundViewStore` 渐进封存要求三元组齐备（`toolsCaptured=false`）、原生请求原子直封并深拷贝（`toolsCaptured=true`）、序列化符合 wire JSON 规范、`discardSealed` 单次排空、`clearSession` 幂等；`ResetScopeTool` 无封存快照时拒绝并给出明确错误，有快照时正常归档并排空。 |

## 跨包物理测试

条款 033、034、039、040、041、043 的真实宿主行为由 `requirements/host-boundary/tests/` 承担：

- `scope-hni-physical.test.mjs`：真实注册的 OpenCode transform 与 `before_provider_request` 上验证首次估计索取、reset 应用、`session.deleted` 隔离、归档 blob 写入失败与 `appendAgent` 失败时内存与快照不变、同回合一次结算取最小、旧 Scope thinking 退场。
- `omp-scope-seam.test.mjs`：OMP 侧 resetScope 注册与协议文本、system 投影、reset 前独立强 steer 而不改用户／工具字节、闭包 Scope thinking 退场、`askPreviousScope` 注册与无 durable 前证据时拒绝伪造、原生 provider hook 冻结旧目录后回放只读 side turn、`session_shutdown` 清理。

## 运行方式

改动后先构建，再用 `node requirements/verification-system/tests/run.mjs` 跑本目录；只想跑本包时用 `TESTS_MJS_FILES` 指定文件。宿主物理测试属于 host-boundary，用 `TESTS_MJS_FILES` 单独指定。

## 已并入正文的历史编号

`003、035、036、037、042、044、045、046、047、048、050、051、052、053` 已无独立测试与法则引用，其仍然有效的行为并入 017、030、033、034、038、043 的正文；这些编号不再作为条款标题出现，测试标题里出现的旧 `WHAT[context-compression-0NN]` 只是历史标签。
