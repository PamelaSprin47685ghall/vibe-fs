# WHY — provider-projection

## 一句话理由

已决定可见的 typed semantic intent，必须经**唯一、确定性**的投影变成 provider
representation；representation 绝不能反向创造 authority 或 state。

## 不可替代性：为什么不能并进别的包

- **不是 `participant-horizon`**：horizon 回答「什么信息有资格进入 participant
  experience」；projection 回答「已经决定可见后，怎样确定性表示」。horizon filter 当前
  落在 projection 路径里只是 implementation fact（HANDOFF §7.2）。
- **不是 `provider-language`**：language 回答「这个 life 说哪种语言」；projection 回答
  「semantic intent 以什么字节形状呈现」。
- **不是各 feature owner**：Repair/Review/Todo/Companion/Strength intent **是否应该
  存在**归各 owner；「它们以什么顺序、怎么合并、冲突时怎么失败」归本包
  （`07-projection.md` DOES NOT OWN）。
- **不是 `prefix-stability`**：前缀 byte 稳定性由 prefix-stability 拥有；本包提供
  intent 模型与 renderer，投影输入不因装配顺序漂移。

## 失败模式（RED 长什么样）

1. **同样 semantic intent 集因装配顺序得到不同 provider 世界**：注册顺序隐式选边——
   同一 intent 集换个装配顺序产出不同前缀。
2. **冲突静默选边**：两条 intent 同时改同一锚点而无显式合并律 → 必须
   `ProjectionConflict`；静默让「先注册者赢」就是 RED。
3. **representation 被反解析成 authority/state**：把 wire 反解析回 Semantic 当 digest、
   把 synthetic role 当成真实 HumanRoot/Opening/completion、把结果 TOML 反解析回控制流。

历史病灶（`docs/why/projection.md` 备选与被拒）：

- 各功能直接改 `Message list` → Seal/digest/前缀稳定性被隐式破坏，无法做 intent 冲突检测。
- Wire 与 Semantic 混用同一相等键 → 要么 Review 假确认、要么 canary 永不命中。
- digest 从 TOML/wire 反解析 → instruction header 追加、seal、transport-only 剔除会让
  反向解析到的「正文」与 canonical 语义漂移。
- 投影层承担生命周期 → 长出第二套编排运行时（Program AST 反模式）。

## 独立变化测试

替换 TOML/wire renderer 或 planner，只要 semantic intent、horizon 与 equality contract
不变 → 本包可独立重大变化；反之亦然（`07-projection.md` INDEPENDENT CHANGE）。

## DEPENDS ON（`requirements-design/INDEX.md` 依赖骨架唯一来源）

- `participant-horizon`：投影输入是「已获准进入 experience」的最小事实集；admission
  过滤是前提。
- `provider-language`：投影产出的是已本地化 prose 的 representation（layout/escaping
  只拥有 representation，不拥有语言）。
