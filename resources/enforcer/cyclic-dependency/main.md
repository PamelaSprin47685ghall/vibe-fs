# cyclic-dependency — Main

## What To Do Now
Break the smallest semantic cycle by extracting the shared fact, protocol, or policy into an owner both sides may depend on without depending on each other.

## Why This Matters
Cycles destroy locality. To understand A you must understand B, but to understand B you must already understand A. Construction inherits the same paradox through lazy initialization, mutable registries, or runtime lookups. The code can execute, yet no component has an independent definition.

## Repair Strategy
Draw dependency arrows by meaning, not file references. Decide which direction reflects authority. Move shared abstractions toward the stable foundation or introduce message/data contracts that let peers communicate without importing each other.

## Wrong Fixes
Do not hide the cycle behind dependency injection, a global service locator, dynamic import, or callback registry. Those techniques can erase a static edge while preserving mutual semantic ownership.

## Verification
The module/package graph should be acyclic, initialization should have a clear order, and either side should be testable against a contract without constructing the other’s implementation.

## Done When
Dependencies form a one-way explanation of the system: lower concepts can be understood before higher ones, and no component requires its dependent in order to define itself.
