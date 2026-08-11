# framework-tax — Main

## What To Do Now
Strip away framework mechanisms that do not carry real domain or operational value and expose the underlying operation through the simplest native construct that fits.

## Why This Matters
Every framework concept consumes reader attention before business reasoning begins. When that cost is not repaid by eliminated complexity, the architecture becomes a tutorial for the framework rather than a model of the system.

## Repair Strategy
Identify what the framework is actually buying—lifecycle, discovery, interception, resource management, portability. Retain only benefits the product needs, replacing the rest with direct functions, modules, language features, or small explicit boundaries.

## Wrong Fixes
Do not build a custom micro-framework that recreates the same ceremony under local names. The goal is fewer concepts, not ownership of the same concepts.

## Verification
A reader should be able to reach the domain operation without traversing configuration or lifecycle machinery unrelated to its semantics.

## Done When
Framework concepts are proportional to the complexity they remove, and the dominant structure of the code is the domain rather than the framework.
