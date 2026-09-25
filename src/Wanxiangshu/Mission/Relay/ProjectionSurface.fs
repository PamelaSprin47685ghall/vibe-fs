namespace Wanxiangshu.Mission.Relay

module ProjectionSurface =
    /// The provider view keeps the full physical history: after a
    /// retirement the next iteration sees every prior message, the
    /// retirement tool call itself and its own fresh-head prompt. Audit
    /// and provider therefore share one message set, and the retirement
    /// cut is only a request-identity test for stale attempts, never a
    /// filter over the provider message set.
    let projectMessages (messages: obj array) =
        box
            {| audit = messages
               provider = messages |}
