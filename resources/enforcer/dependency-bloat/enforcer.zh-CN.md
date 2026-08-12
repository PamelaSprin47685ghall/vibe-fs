# dependency-bloat — Enforcer 中文版

## 定义
Dependency bloat 不是“第三方包越少越好”。真正的问题是：为了省掉一个很小、很稳定的 capability，本地项目把整个外部 ecosystem 的升级、安全、transitive graph、runtime assumption 与迁移义务一起买了进来。

Dependency 的成本不是今天少写多少行，而是未来多少年必须跟着别人一起变化。

## 何时触发
- platform 标准库已有能力，却再引入大型 package/framework；
- 为一个小 helper 引入几十个 transitives；
- package 带来 runtime/config/build system 变化，远大于 domain value；
- “大家都用这个库”替代了具体 capability/lifetime cost 分析；
- 升级/漏洞/兼容工作成为长期税，但最初问题本可小规模直解。

## 不要误判
- cryptography、TLS、parser、codec、标准协议等复杂且安全敏感的领域，成熟依赖通常比自造更负责；
- 已在 dependency graph 中、无需扩大 surface 的现有 capability；
- 独立快速演进 specification 不应复制进仓库；
- 小而专一的库若真正封装困难复杂度，可以值得。

## 刀口
比较的不是“dependency 代码行数 vs 我写几行”，而是：**它在生命周期内替我们拥有了多少真实复杂度，又额外带来多少非问题本身的义务？**

## 与近邻区分
`framework-tax` 是已经采用后 ceremony 主导设计；这里审最初 acquisition decision。

## 提醒
依赖是借来的代码，也是借来的未来。只有当它替你承担的复杂度比它带来的未来更贵时，才值得买。
