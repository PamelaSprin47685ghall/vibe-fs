A program may mutate each canonical path exactly once. A second rewrite/write
on the same path is DUPLICATE_MUTATION_TARGET. Multi-phase edits belong in
JavaScript variables, then one rewrite/write.

The generated class has no commit, rollback, snapshot, or transaction methods.
run() returning normally → Host preflight → prepare → commit. run() throwing or
any file()/glob()/grep() failure discards every staged mutation.

run() must return a JSON-compatible value: null, boolean, finite number, string,
array, or plain object (recursive). undefined, BigInt, NaN, Infinity, function,
symbol, cyclic or exotic objects fail as INVALID_RETURN_VALUE before commit.
