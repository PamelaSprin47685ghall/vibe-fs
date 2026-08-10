# Persist — 理由

动态 durable 状态只允许有一个解释权：统一 EventStore。否则每个 feature 会再发明一份 journal / blob 目录 / 私有 ref / 合并协议，崩溃恢复与多进程合并立刻变成产品分叉。

Git raw ODB 是物理介质，不是第二真相：事实是 event；大正文是 content-addressed blob；原子发布是 `refs/wanxiang/store` 的 CAS。历史来自 event DAG，不来自 commit / branch / tag。

Clean-break：旧 NDJSON journal、RuntimePath `blobs/`、Student QA 私有文件与其它 feature-owned store **不要求可读、不迁、不双写**。runtime 永不打开它们（leave-unread）。AgentJournal 只是 EventStore 上的适配表面，不是平行存储。

## 备选与被拒

**多存储 vs 单一 durable substrate。** 拒 feature 自有 journal/blob/ref：同一仓库多进程与 dumb remote 无法共享一套 merge/CAS，恢复路径按 feature 分裂（PERSIST-005/006）。

**先改内存再补盘 vs append 成功后才改权威态。** 拒内存优先：内存会看见无证据的未来；崩溃后重放与内存分歧进不了恢复路径。必须 `Append`/`Publish` 见证成功后再 fold 投影（PERSIST-002/003）。

**schemaVersion / store-v2 vs 无存储版本 + additive event vocabulary。** 拒 envelope/store 版本链：版本不是领域事实，会逼出永久 migration mode。已 committed 的 `event_type` 语义冻结；新语义用新类型（PERSIST-001/005）。

**全历史扫描 vs O(1) projection。** 拒全扫：把「查询」变成「重放成本」。投影是积分状态；恢复路径可控（PERSIST-008）。

**副作用：Requested→Accepted vs 内存「好像做了」。** 拒内存记账：外部副作用必须可在崩溃后按效果身份核对。Requested-only 表示结局未知，不表示未发生；Accepted 不得折回（PERSIST-009）。

**内容寻址 payload vs 自增 id / 目录 blob。** 拒自增与第二套 RuntimePath blob：重放会漂身份。Git object id 即物理 identity；Domain 只见 opaque `PayloadRef`（PERSIST-007）。

**Student QA 私有权威文件 vs 统一 store / 退休。** G3 已删除该域；不得迁入 EventStore，不得发明后继 QA vocabulary（PERSIST-011 空缺）。
