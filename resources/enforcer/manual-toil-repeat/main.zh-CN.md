# manual-toil-repeat — Main

把重复机械步骤搬给 machine，把真正 judgment 留给人。

先把 ritual 拆成两类：

- deterministic：输入固定后，正确结果唯一；
- judgmental：需要理解语义、权衡 tradeoff、处理 ambiguity。

只自动化前者。把它接进正常开发入口：build/check/generator/migration tool/release script，而不是再造一个“记得偶尔手工运行”的脚本。

常见假修复：

- 把 checklist 写得更详细；
- 新增一个脚本，但仍靠人记得何时跑；
- automation 失败只 warning，最后仍靠 reviewer 人肉判断有没有漏；
- 把需要 semantic judgment 的内容硬编码成 regex/keyword gate；
- 自动修改 baseline/snapshot 直到 check green，把 evidence gate 变成自我赦免；
- 只自动 happy path，失败后仍需要复杂手工 cleanup，却没有明确 ownership。

好 automation 应 fail closed、输出 precise cause，并尽可能 idempotent/reproducible。若会写文件，明确 generated ownership，避免 generated 与 hand-edited 两个 truth。

验证 automation 最好的方式是故意漏步骤/制造 stale artifact：标准 check 应自己发现或生成正确结果，不需要 reviewer 记住隐藏 ritual。Clean checkout 也应能执行，不依赖某个人机器上的 shell history。

对不能自动化的 judgment，不要因为“想减少 toil”就伪结构化。可以提供 evidence collection、diff、summary，降低人类搜索成本，但最后 verdict 仍归真正 owner。

完成时重复劳动要么消失，要么只剩确实需要人类理解的部分；“请记得每次手工做 X”不再承担 correctness。

> 最好的 automation 不是让机器替人思考，而是让人终于不用把注意力浪费在不需要思考的地方。