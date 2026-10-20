Declare the next view with both `prefixUntil` and `thenMap`. The prefix protects anchors below it from replacement; raising or lowering it does not restore earlier material. `thenMap` maps non-negative anchor numbers to null (keep) or a non-blank summary. Omitted anchors remain unchanged.

A summary replaces the interval from that live anchor to the next live anchor. Each operation captures its bounds before this declaration is applied, so adjacent summaries remain separate and later messages stay outside an old tail replacement. Replacement history accumulates in order. A replaced real anchor ceases to exist; implicit anchor 0 always addresses the opening interval, including earlier summaries.

Only user messages and terminal tool results carry inline `[ANCHOR: N]` markers. Plan real user originals cannot be replaced; ignored attempts are reported. Provider projections do not rewrite the original messages or Trace.

Numbers and summaries last only in this process. A durable checkpoint records call identity, not view material. Tenure isolation is not reversible through a view. Keep lasting knowledge in P.
