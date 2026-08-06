# Projection — 理由

禁止各功能直接改 Message list，否则 Seal/digest/前缀稳定性被隐式破坏，且无法做 Intent 冲突检测。

Wire 与 Semantic 分型：字节相等键与语义相等键混用，要么 Review 假确认，要么 canary 永不命中。

DSL 不负责生命周期，避免投影层长出第二套编排运行时——投影只回答「此刻 provider 应看见什么」。
