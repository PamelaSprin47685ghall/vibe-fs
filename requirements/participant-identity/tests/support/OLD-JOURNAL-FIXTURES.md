# GAP-159 old journal fixtures

These are unchanged NDJSON bytes produced by the clean fixed generation667 npm package, SHA-256 `b0de9b9770139ae4aa977fae3d8b8d59f2d558cf4ff4a41cf4a1289419743297`. No root or cut is fabricated. Only disposable test workspaces and identities are present.

| File | Actual old producer | SHA-256 |
|---|---|---|
| old667-mixed-journal.ndjson | Public Plugin chat admitted Engineer, Manager and the old Predictor alias; eight events | a9709c18a6a1a6256f04f0acf384945d002b5904a90dc8f2c4577fc1a8c36ef5 |
| old667-predictor-later-cut.ndjson | Dispatcher admitted Predictor; real Journal linked one handle to two children; producer settled bad+cut and exited SIGKILL; five events | 7a6ca9b312e0bb1b38546b521dc06390a7716bd56c4adf71903318105a3f0008 |
| old667-same-event-cut.ndjson | Two real concurrent root requests: Engineer installed, conflicting Predictor rejected with its own durable cut and producer SIGKILL; four events | 51577bb621c510efd797db610b352071bb4aa00ddb8a40a8cb8d3cc8327d94fb |

The child scenario anchors only its wall clock to the retained UTC day, before importing the product. Monotonic time and timers remain physical and continue advancing. The original ObservedAt, roots, causal parents and cuts are never rewritten or refreshed. Each child restores the bytes into a fresh Git workspace before the actual Plugin server starts. This keeps the regression independent of the day on which it is run, without changing product retention or adding a test hook.

The first two fixtures must refuse a fresh Engineer identity. The third must retain the valid Engineer and allow its exact root to continue. Source, producer SIGKILL, old/new controls and hashes are retained in the GAP-159 repair evidence archive.
