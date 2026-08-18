# requirement-grounding

## 一句话 WHY

开发者第一次阅读或修改一段代码时，相关 requirement 不能等到事后才被想起；万象术应按
项目自己的 `requirements/<package>/` 覆盖关系，把适用包的规范文档与测试源码自动带进当前
参与者上下文，并对同一内容版本只 grounding 一次。模型看到的 grounding 不是新消息类型，而是
与它自己调用 `read` 完全相同的一组文件读取。

## WHAT 概览

- **项目可复用**：能力面向任意采用 `requirements/<package>/` 风格结构的工作区，不把
  万象术仓库路径写死进运行时。
- **正向覆盖**：包目录自身天然覆盖；可选 `APPLIES-TO` 只声明包外路径，使用 gitignore
  wildmatch 写法但采用正向语义：普通行纳入，`!` 行排除例外。
- **触碰即关联**：真正把源码内容交给 provider，或准备对文件产生 mutation 时，按实际路径
  求出全部适用 package。
- **read 等价**：ordinary provider 只看到正常 `read` tool-call/tool-result，路径、range、裁剪、错误与
  来源语义都复用模型主动 read 的同一实现；Cursor 是唯一形状特判。
- **永久投影**：第一次自动 read 的 call/result 原始字节与 gap anchor 进入 durable history；后续轮次只
  原位重放，不重新读文件、不重算、不移动，因此旧 provider wire 始终是新 wire 的稳定前缀。
- **固定落位**：与 pair-programming 伪 `skill` 在同一追加点出现时，顺序固定为“伪 skill → requirement
  reads”；新 digest 只在当前尾部追加新 reads，历史 reads 永不改写。
- **Cursor 同源特判**：Cursor 不伪造 read call，只在 terminal result 的 `NUL+BOM` suffix 中先保留伪
  skill payload、再追加 requirement read results；每段 result 用稳定 source-path attribute 标明来源文件，正文保持
  ordinary read 原字节。
- **读与改不同**：第一次读取可在同一 continuation 补做这些普通 read；第一次修改若发现
  未 grounding 的 package，必须在 effect 前停下，先完成普通 read，下一次明确修改才可执行。
- **一次性**：grounding identity = workspace + package + package-content digest；同一 participant
  context 已交付同 digest 不重复。包内容变化后产生新 identity，可重新 grounding。
- **完整材料**：material set 包含存在的 README/WHY/WHAT/HOW/PROOF、`APPLIES-TO`，以及
  `tests/**/*.test.mjs` 测试源码；稳定排序，不从散落源码猜规范。
- **多包并存**：一个路径可命中多个 package；全部未 grounding 包按包名稳定排序后依次走普通 read。
- **工具无关**：OpenCode native read/edit/write/grep/move/remove 与万象术 repository programming
  surface 服从同一语义门，不能换工具绕过。
- **知识不是授权**：自动 grounding 是普通文件读取知识，不是 user message，不创建 interaction authority，
  也不扩大角色 capability。

## 当前状态

本包本轮只落正式语义、路径声明和待实现契约测试。OpenCode runtime、scope resolver、read-equivalent
adapter 与 mutation gate 尚未实现；事实缺口见 `PROOF.md` 的 GAP-017..020，以及
`requirements/GAP.md` 聚合台账。

## 阅读顺序

1. `WHY.md` —— 为什么“写完再看 requirements”在自动化开发里必然太晚。
2. `WHAT.md` —— 当前能力必须成立的正式合同。
3. `HOW.md` —— 推荐的 resolver / material planner / read-equivalent OpenCode gate 结构。
4. `PROOF.md` —— 每条 WHAT 的测试落点与当前 GAP。
5. `tests/` —— 待实现契约测试。
6. `APPLIES-TO` —— 本包额外覆盖的包外实现面；本目录自身无需声明。

## DEPENDS ON

- `requirement-system`
- `host-boundary`
- `participant-horizon`
- `provider-projection`
- `interaction-authority`
- `semantic-trace`
- `prefix-stability`
- `repository-programming`

