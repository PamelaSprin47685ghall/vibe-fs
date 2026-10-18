# Tool · deliver

End the current incumbency, or start an `S4` execution session on user command in the `post-S3` state.

## Contract and Usage

1. **Precondition**: the incumbency must hold no live blocking resources (a pending `devops`, background work, unsettled tool calls). With blockers present the call is rejected with a blocker summary; settle them first. An unanswered user `ask` is an explicit wait, not completion.
2. **Relay (No Authority)**: before relaying, declare `view` so the successor keeps what it needs; `deliver` retires this incumbency and opens the next one at the same stage. Relay does not advance the stage and produces no review. The next runner starts again from `U`, your declared `view` and `P`, inspecting your `P` output and the declared view read-only first. A single unreviewed relay is allowed, but the kernel refuses a relay that would be the second in a row with no review in between (`assessment required`, no write): a chain of unreviewed relays never advances the stage. Submit this incumbency's `review` first, then relay.
3. **Authorized Closeout (Enter `post-S3` State)**: after `S3` passes review and grants authority, calling `deliver` confirms `P` is non-empty, retires this incumbency, permanently disables nudges for this session, enters the `post-S3` state, and interrupts the conversation (like `ask`) to wait for the user without immediately starting `S4`.
4. **Start `S4` in `post-S3` State**: in the `post-S3` state, when the user enters a command asking to execute `deliver`, calling `deliver` starts a parentless `S4` (Manager) execution session with the full text of `P` as the init prompt and presents the receipt. The user may repeatedly request starting `S4`, and each `deliver` call launches a new `S4` execution session.

Delivery is forbidden while `P` is empty or missing; the delivery path is forbidden without authority.
