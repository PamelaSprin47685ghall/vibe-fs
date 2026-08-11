# expected-failure-as-exception — Main

## What To Do Now
Move foreseeable business failures into a closed typed result and make callers match the cases explicitly.

## Why This Matters
An exception says the normal contract was interrupted. “Insufficient balance” or “not authorized” is usually not an interruption; it is one of the contract’s legitimate answers. Encoding it exceptionally makes the API claim a simpler world than the business actually permits.

## Repair Strategy
Name each expected refusal in domain language, return it beside success, and translate to HTTP/UI/provider representations only at the outer boundary. Keep unexpected infrastructure failures separate.

## Wrong Fixes
Do not return magic nulls, booleans, or error strings instead. Those avoid exceptions while still erasing the domain alternatives from the type.

## Verification
Callers should fail to compile or test if they ignore a newly added business outcome, and frontends/adapters should map typed cases without parsing prose.

## Done When
The function signature tells the truth about every foreseeable business outcome, and exception handling is reserved for failures outside normal domain choice.
