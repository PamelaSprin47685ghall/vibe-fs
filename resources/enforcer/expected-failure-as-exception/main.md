# expected-failure-as-exception — Main

## What To Do Now
Move foreseeable business failures into a closed typed result and make callers match the cases explicitly. The operation’s closed result type is who owns every foreseeable business refusal; the exception channel is not.

## Why This Matters
An exception says the normal contract was interrupted. “Insufficient balance” or “not authorized” is usually not an interruption; it is one of the contract’s legitimate answers. Encoding it exceptionally makes the API claim a simpler world than the business actually permits.

## Repair Strategy
Name each expected refusal in domain language, return it beside success, and translate to HTTP/UI/provider representations only at the outer boundary. Keep unexpected infrastructure failures separate.

## Decision Branches
- If the product can name the refusal in advance, put it in the result type and force callers to match it.
- If the failure makes domain reasoning impossible, keep it exceptional and do not pretend it is a business case.
- If throw/catch encodes loop control or not-found plumbing rather than a named refusal, use `exception-driven-control-flow`.

## Common Wrong Fixes
- Do not return magic nulls, booleans, or error strings instead.
- Do not catch the business exception at every call site and map it to the same string.
- Do not widen a generic `AppException` hierarchy and call that a contract.
- Do not leave core APIs throwing while only the HTTP layer “knows” the cases.

## Verification
Callers should fail to compile or test if they ignore a newly added business outcome, and frontends/adapters should map typed cases without parsing prose. The invariant is that the function signature tells the truth about every foreseeable business outcome.

## Done When
The function signature tells the truth about every foreseeable business outcome, and exception handling is reserved for failures outside normal domain choice.
