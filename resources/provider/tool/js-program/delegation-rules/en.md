fork(payload) — delegate one charge to a child work. Payload fields match the host fork tool:
`charge` (required, the assignment text), `name` (stable child name), `calling` (which role
takes the charge) and `keywords`. The call returns the fork receipt text; the child runs in
its own session and its terminal result is collected by join.

join() — wait for the children this session created and return the join receipt batch.
Membership-scoped: there is no handle list to pass. Fork several charges, then join once per
wave; a join with nothing pending returns the empty-join receipt.

Both members reflect the host delegation contract exactly: failures come back as receipt
text, not as thrown errors, so read them in the program. Delegation is available only where
the attempt holds the Fork/Join capability.
