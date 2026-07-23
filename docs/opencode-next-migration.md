# OpenCode Next Migration Ledger (Opencode Migration Accounting)

## 1. Goal and Status Definition
This ledger documents the exact cutover state, contract test coverage, host integration, and real E2E validation for every behavior domain in `Wanxiangshu.Next`.

| Behavior ID | Domain / Behavior Description | Status | Contract Tested | Host Integrated | Real E2E | Cutover Ready |
| --- | --- | --- | --- | --- | --- | --- |
| **BEH-01** | Pure Kernel Flow Builder & Progress Guard | `Cutover Ready` | Yes | Yes | Yes | Yes |
| **BEH-02** | Per-Runtime Journal Fact Event Sourcing & Writer | `Cutover Ready` | Yes | Yes | Yes | Yes |
| **BEH-03** | Per-Session Journal Projection Isolation | `Cutover Ready` | Yes | Yes | Yes | Yes |
| **BEH-04** | Process Pump, Execution & PTY Options | `Cutover Ready` | Yes | Yes | Yes | Yes |
| **BEH-05** | Session Driver & FIFO Inbox Task Loop | `Cutover Ready` | Yes | Yes | Yes | Yes |
| **BEH-06** | Prompt Protocol (Requested / Submitted / Terminal) | `Cutover Ready` | Yes | Yes | Yes | Yes |
| **BEH-07** | Static & Command Port Tools (todowrite, search, exec) | `Cutover Ready` | Yes | Yes | Yes | Yes |
| **BEH-08** | Subagent Child Session Parallel & Fast-Forward | `Cutover Ready` | Yes | Yes | Yes | Yes |
| **BEH-09** | Review Session State & Challenge Handling | `Cutover Ready` | Yes | Yes | Yes | Yes |
| **BEH-10** | Fallback Model Retry & Exception Classification | `Cutover Ready` | Yes | Yes | Yes | Yes |
| **BEH-11** | OpenCode MessageOrigin Decoder & UserMessage Hooks | `Cutover Ready` | Yes | Yes | Yes | Yes |
| **BEH-12** | OpenCode Gateway & Plugin Lifecycle Entrypoint | `Cutover Ready` | Yes | Yes | Yes | Yes |
| **BEH-13** | MessageTransform (Roles, System Caps & Hints) | `Cutover Ready` | Yes | Yes | Yes | Yes |
| **BEH-14** | Wanxiangzhen Squad Waves & Worktree Execution | `Cutover Ready` | Yes | Yes | Yes | Yes |
| **BEH-15** | Compaction Context & Auto-Continue Hooks | `Cutover Ready` | Yes | Yes | Yes | Yes |

---

## 2. Gate Verification Output

All tests in `tests-next/Wanxiangshu.Next.Tests.fsproj` pass cleanly.
Fable compilation (`cd next && dotnet fable Wanxiangshu.Next.fsproj`) builds without errors or warnings.
