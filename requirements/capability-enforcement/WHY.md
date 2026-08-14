# WHY — 为什么 `capability-enforcement` 必须独立存在

## 不可替代的存在理由

office 的 authority 写在 consequence 模型里（`office-capability`）。但如果 provider-visible schema
与 runtime execution gate 各自维护一份能力表，就会出现两类分叉：

```text
schema 有、gate 无   → provider 看得见工具、schema 撒谎（调用即失败/越权执行被拒但表面可用）
gate 有、schema 无   → provider 看不见却可能执行（更危险：无资格面被伪造调用命中）
```

真实历史：`changes/completed/js-capability-projected-tools.md` 明确「手写 role→JS 矩阵 vs 从唯一
权威投影」被拒——任何第二份矩阵必然与权威漂移；`docs/why/agent.md`「双层权限 vs 单层可信」被拒——
Host 配置可漂，只信一层会在配置异常时漏工具或越权执行。`docs/why/js-tools.md` 的四层同构
（capability → base-class member → description → example → runtime gate）是同一律在编程面的应用：
模型看到的与可执行的必须完全一致，不需要读权限矩阵。

本包把「同源 + 不扩权」钉成独立合同：schema 层与 gate 层读**同一** capability truth；projection
只能按 office + request contract 收窄，任何执行档（tier / replica / leaf）不得扩大 office
entitlement。

## RED 是什么样

- schema 与 runtime gate 漂移（某工具只见于 schema 或只见于 gate）。
- 某 execution tier（fast/deep/replica/leaf）获得比同 office 其它档更多的 authority。
- internal-only participant/action（Blogger/Distiller/Bookkeeper）能被无资格 participant 合成/执行。
- Host 配置异常时权限写失败，managed agent 回落到 Host 默认（如 `bash` 开放）。
- 手写第二份 role→工具矩阵；`js-*` 表面四层不同构（description 有、runtime 拒绝）。

## 为什么不并进相邻包

- `office-capability` 答「有资格产生什么后果」（entitlement 语义）；本包答「schema/runtime gate 如何
  同构、如何保证不扩权」（enforcement 语义）。二者是独立 WHY（INDEX「关键拆分裁决」：新增
  capability-enforcement，office consequence 与 schema/runtime gate 同构是两个不同 WHY）。
- `participant-identity` 提供身份轴（谁在行动）；本包消费它，但 gate 同构不是身份问题。
- `repository-programming` 应用同构律到编程面（JS SDK）；律本身唯一归本包（COVERAGE「1 OVERLAP
  修复」：同构/同源律唯一归 capability-enforcement，repository-programming 只应用）。
- `participant-horizon` 答「什么信息有资格被看见」（admission）；本包答「看见的与能执行的如何
  一致」（执行面）。internal participant 不进无资格 choice surface 的 admission 归 horizon，其
  schema/gate 拒绝归本包。
- 独立变化测试：把 ToolPermission Set 改成 capability tokens/traits，并重写 Host/plugin gate，
  而 office authority 与 participant-visible action contract 不变——本包必须能单独承受
  （boundary card INDEPENDENT CHANGE）。

## 一句话

看见的 = 能执行的，且不扩大 office entitlement。同源是唯一防漂移手段。
