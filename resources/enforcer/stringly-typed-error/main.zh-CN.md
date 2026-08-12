# stringly-typed-error — Main

## What To Do Now
用 producer 或 protocol adapter 拥有的闭合 error identity 替换 message parsing。内部只按 typed case/code 分支；人类可读文本在 semantic case 已知之后再渲染。

如果 upstream 只有 prose，就在**唯一 external boundary** 做一次分类，保留 raw message 作 evidence，并返回 `RateLimited | Unauthorized | Timeout | Unknown raw` 一类内部结果。

## Why This Matters
人类文案本来就应该改变：诊断会加上下文，localization 会整句变化，provider 会改措辞，标点也会调整。这些改进都不应该偷偷改变 retry、authorization、fallback 或 recovery policy。

Stringly error 把 copyediting 变成 control-plane mutation。

而且字符串分类很容易过度自信：一句话里出现 “timeout” 不等于事件身份就是 timeout。若 producer 没给 structured identity，adapter 最多只能做一个有边界、有 `Unknown` 的启发式分类，不能假装 prose 已经是可靠协议。

## Repair Strategy
1. 找出所有按 error prose 做机器分支的地方。
2. 命名这些 branch 真正需要区分的 semantic outcome。
3. 在最早知道该区分的 owner 定义闭合 case/code。
4. 外部 text-only error 只在 adapter 映射一次，并保留 `Unknown raw`。
5. formatting/localization 全部移到外层。
6. 控制流测试改断言 typed identity；只有 copy 本身是需求时才单独断言 prose。

## Decision Branches
- 自己控制 producer：直接输出 structured identity。
- 不控制且 upstream 只有文本：把不可避免的 classifier 困在一个 adapter，并显式保留 uncertainty。
- outcome 本来就是 expected ordinary control flow：identity 修好后再看是否还有 `expected-failure-as-exception`。
- string 纯诊断、没人解析：不要动。

## Common Wrong Fixes
- 把所有 regex 搬进 `ErrorUtils` 就宣布解决。
- 冻结当前英文句子当永久 unofficial protocol。
- provider 每改一次文案，就继续追加 substring special case。
- 把所有 unknown message 都硬映射成某个 confident case；假确定性比 `Unknown` 更危险。
- typed identity 已存在后，控制流 test 仍冻结 exact human message。

## Verification
保持 typed case 不变，改标点、加上下文、切换 EN/zh-CN：机器行为必须完全相同。

再保持 prose 很相似，只改变 typed case：控制行为必须跟 case 走，而不是跟关键词碰撞走。

Invariant：**control semantics 与 human wording 独立。**

## Done When
内部机器决策不再依赖 rendered error prose；不可避免的外部文本分类只有一个明确 owner；错误文案可以自由改进和本地化而不影响 recovery、authorization、routing。