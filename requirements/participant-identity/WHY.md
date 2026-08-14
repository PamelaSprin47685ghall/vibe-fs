# WHY — 为什么 `participant-identity` 必须独立存在

## 不可替代的存在理由

系统里有三类「这个人是谁」的答案，历史上被绑成一个词，导致三种完全不同的失败：

```text
Role              = office 身份：你属于哪个职位、负有哪类职责（Coder / Manager / …）
Persona           = 自我模型：你自称什么、怎样理解自己（Engineer / Lead / …）
ExecutionBinding  = 物理执行者：当前用哪个模型、哪个 tier、哪份 config（fast-coder / deep-coder）
```

如果三者合一或两两混淆，换执行机制会偷偷改变人格与责任。真实事故（历史 why/agent 条款、
历史 change（universal））：

- **Bookkeeper 用 `fast-inspector` 创建却收 Inspector prompt**：机器身份（binding）与自我模型
  （persona）合一，身份轴错位。
- **Peer Fallback 换模型时半途换人**：fast→deep 本应只是换执行者，却被读成换了一个人，
  responsibility / self-model 随之漂移。
- **Strength replica 漂出独立自我**：replica 只应换执行绑定，却可能演化成「另一个自己」。

本包把这三条轴钉死为独立事实：谁在行动（Role）、行动者如何自称（Persona）、谁来物理执行
（Binding）。三者可独立变化，且只有 Binding 允许在 life 内变化。

## RED 是什么样

- 换 model / execution context 能偷偷改变 responsibility 或 self-model。
- 新 participant 与同一人的另一 execution context 无法区分（换了执行者 = 换了一个人）。
- Persona 被重绑（Fallback / Strength / mid-life 改写自称）。
- 用户面 session 的 execution binding 被内部 prompt / hook 静默改写，冒充用户选择。
- `fast-`/`deep-` 机器名出现在 provider 视野里自称「我是 fast-coder」。

## 为什么不并进相邻包

- `office-capability` 答「这个 office 有资格产生什么后果」；本包答「谁在行动」。同一 Role 名
  跨 Persona/Binding 不变，是身份事实，不是后果事实。
- `capability-enforcement` 答「可见/可执行 capability 如何同源不漂移」；身份轴是它的输入前提。
- `session-ontology` 答 session 的 execution class / ownership / attachment 分类；本包消费它的
  personhood 轴，但不拥有分类本身。
- 独立变化测试：把 Persona 从 `Role × initial tier` 改成显式创建时选择，而 office capability 与
  provider renderer 完全不动——本包必须能单独承受这次变化（boundary card INDEPENDENT CHANGE）。

## 一句话

换执行者 ≠ 换人。三者分离是可单独验收、可单独破坏、不可替代的身份合同。
