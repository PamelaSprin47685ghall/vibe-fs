# Projection — 理由

禁止各功能直接改 Message list，否则 Seal/digest/前缀稳定性被隐式破坏，且无法做 Intent 冲突检测。

Wire 与 Semantic 分型：字节相等键与语义相等键混用，要么 Review 假确认，要么 canary 永不命中。

DSL 不负责生命周期，避免投影层长出第二套编排运行时——投影只回答「此刻 provider 应看见什么」。

## 备选与被拒

**Intent 冲突解决：隐式注册顺序选边 vs 显式合并律矩阵 + Fail-Closed。** 拒绝依靠 intent 注册顺序隐式解决同锚点冲突：这会导致非确定性 Bug（如 `activatePrefixEpoch` 与 `keepPhysicalPrefix` 竞争时按调用顺序产出不同前缀）。选择定义显式 7x7 合并律矩阵，对互斥组合无条件返回 `ProjectionConflict` Fail-Closed，并用 Property-Based 测试约束合并函数的结合律与交换律。

**投影形态：裸改 `Message list` vs typed 组合子/CE。** 拒裸改：Seal/digest/前缀稳定性被隐式破坏，无法做 Intent 冲突检测（PROJ-006）。选 typed intent + 纯管线（PROJ-001）：功能只声明意图，渲染器收敛字节，编译期拦非法组合。

**digest 来源：parse TOML / wire vs Semantic 投影。** 拒反解析：多处变化（instruction header 追加、seal、transport-only 剔除）会让反向解析到的「正文」与 canonical 语义漂移。选 Semantic 投影直接对不可变树算 digest（COMPANION-007）。

**投影分层：单类型 vs Semantic/Wire 双型。** 拒混用：字节相等键与语义相等键不互换，否则要么 Review 假确认、要么 canary 永不命中（VERIFY-007）。选双型，各用其境：seal/前缀缓存走 Wire（含 ID、字节相等），canary/delta 走 Semantic（去 ID、语义相等）。

**PERT回：DSL 是否承载生命周期。** 拒：启动 Replica、等待 provider、写 Journal 属 Coordinator 职责；投影层若长第二套编排即翻回 Program AST 反模式（PROJ-007）。Service 层只映射输入快照→输出先验确定性投影。

