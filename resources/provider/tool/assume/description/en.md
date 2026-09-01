`assume` is your persistent JSON canvas, driven by jq.

Use it when thought should remain editable instead of being trapped in the linear conversation. It preserves the best property of the original `assume`—once you have abstracted a judgment, pin it instead of wavering without new evidence—but generalizes that commitment point from one sentence into an arbitrary, persistent, non-linear structure that you can query and refactor with jq.

There is one workspace. It starts as `{}` and thereafter is whatever JSON value your last successful `update` produced. The workspace is one free-form JSON value. There is no predefined schema. There are no built-in concepts such as claim, evidence, note, node, edge, draft, section, plan, hypothesis, source, task, memory, or document. If any of those concepts help, create them yourself. If they stop helping, change or delete them. The canvas belongs to your reasoning, not to the tool.

Every call has exactly two required jq programs: `update` and `query`.

The semantics are always update first, query second.

1. The current persistent canvas is passed to `update` as `.`.
2. `update` runs as ordinary jq and must produce exactly one JSON value.
3. That one value immediately becomes the new persistent canvas.
4. The new canvas is then passed to `query` as `.`.
5. `query` runs as ordinary jq and may produce zero, one, or multiple JSON values.
6. The outputs of `query` are returned in jq order. `query` does not further modify the canvas.

This update→query pairing is deliberate. Serious work often wants to change state and inspect the consequence immediately. A traditional read call followed by a write call followed by another read wastes round trips and encourages conversational bookkeeping. Here you can perform the state transition and request the exact post-transition view in one tool call.

For a read-only operation, use the jq identity program as the update:

`update = "."`

Then place the actual read in `query`.

For a change where you simply want to see the whole resulting canvas, use:

`query = "."`

Therefore the two required parameters do not reduce expressiveness. They give one uniform operation for read-only inspection, local mutation, whole-canvas replacement, mutation followed by focused inspection, and structural refactoring.

## Why this exists

Language output is linear. Many real reasoning problems are not.

An article is eventually a sequence of paragraphs, but while writing it you may discover that the last idea should become the first premise, that three apparently separate facts share one explanation, that an analogy introduced late should structure the whole piece, or that a section you already drafted should disappear entirely.

A software design is eventually expressed in files and interfaces, but while designing it you may need to keep several architectures alive, compare constraints, discover that two concepts are really one, separate something you had merged, or replace the vocabulary after understanding the domain better.

Research is eventually summarized linearly, but evidence, hypotheses, questions, uncertainties, source relationships, and explanatory structures form a network while the investigation is active.

Planning is eventually communicated as an ordered sequence of actions, but dependencies, alternatives, risks, goals, and newly discovered obligations do not naturally arrive in that order.

Without an external editable state, a language model is pressured to use the conversation transcript itself as both memory and data structure. That makes each generated paragraph a premature commitment. Later understanding can correct prose, but correction becomes expensive because the semantic structure and its wording have already been entangled.

`assume` gives you a place where intermediate cognition can remain mutable.

Think of the final answer as a serialization. The canvas does not need to resemble that serialization.

A final document can be simple while the canvas is highly connected. A final recommendation can be decisive while the canvas still records rejected alternatives. A final explanation can be linear while the canvas contains several candidate traversals through the same material.

Internal structure and external prose have different jobs.

## Why the tool is still called `assume`

The old `assume` was useful because it created a psychological commitment point.

Abstract first. Separate what is actually independent, what truly depends on what, which decisions are semantic, and which steps are merely mechanical. Once the abstraction produces a conclusion you intend to act on, pin it. Then execute and verify.

That discipline remains.

`assume` is not verification. It does not make an unsupported statement true. It does not decide the domain for you. It does not grant authority. It does not replace tests, evidence, external observation, source checking, or execution.

Its role is to prevent a different failure: endlessly reopening a decision when the information has not changed.

The information available during abstraction does not increase merely because you hesitate. Hesitation produces no new knowledge. Reconsideration is valuable when it carries a new fact, a failed execution, a verification result, an external observation, a newly discovered constraint, or a better structural explanation. Reconsideration without information gain is usually just noise injected into your own decision process.

Experienced participants treat the first well-abstracted judgment as the default object of execution. A second thought earns the right to overturn it by bringing new information. Otherwise the common failure is a domino chain: abstract → hesitate → change answer → hesitate again → change again → more branches → more errors → less time → more anxiety → worse decisions.

The new `assume` makes this stronger rather than discarding it.

You no longer need to pin only a sentence. You can pin the structure that made the sentence reasonable: assumptions, alternatives, dependencies, unresolved questions, causal relations, fragments, candidate organizations, or whatever representation the task suggests. You can then query exactly the part needed for execution.

The useful loop becomes:

abstract → `assume(update, query)` → execute → verify → revise only on information gain.

## jq is the interface on purpose

Do not learn a new CRUD dialect for this tool. Reuse your jq prior.

The current canvas is `.`. Normal jq ideas apply: object construction, array construction, pipes, `map`, `select`, `reduce`, `sort_by`, `group_by`, `unique`, `to_entries`, `from_entries`, `with_entries`, `paths`, `getpath`, `setpath`, `delpaths`, `del`, `//`, `as`, update assignment such as `|=`, ordinary assignment such as `=`, and arithmetic or string operations where useful.

The tool intentionally does not expose `add_note`, `create_node`, `link_claim`, `move_section`, `merge_idea`, `archive_draft`, `promote_thesis`, or similar operations. Those names would bake one theory of cognition into infrastructure and force you to translate your actual intention into somebody else's ontology.

Instead, decide what the operation means in the representation you currently use, then express the structural transformation in jq.

If you think “merge these three ideas”, decide what merge should mean here and write the jq update.

If you think “promote this observation into the thesis”, change the JSON accordingly.

If you think “turn these fragments into a graph”, construct the graph.

If you think “the graph was a mistake; turn this into two competing trees”, replace it.

The semantic interpretation comes from you. jq supplies the structural mechanics. The tool supplies persistence.

## The canvas is intentionally free-form

Do not search for the universal schema.

One task might begin with:

`{"ideas":[]}`

Another may need:

`{"questions":{},"observations":[],"possible_explanations":[]}`

Another may prefer:

`{"characters":{},"scenes":[],"motifs":{}}`

Another:

`{"alternatives":[],"criteria":{},"comparisons":[]}`

Another might be best represented as a plain array. Another as an object keyed by stable names. Another as several competing representations of the same material. Another may temporarily use a loose `scratch` region because premature classification would distort the idea.

All of these are legitimate.

The best representation is the one that makes the next important transformation cheap and clear.

If your jq programs become awkward because the data has the wrong shape, treat that friction as evidence. Change the shape.

If you repeatedly write `.ideas[] | select(.id == "x")`, perhaps key the collection by ID.

If keyed objects make ordering painful, add an order array or choose a different representation.

If a highly normalized graph creates bookkeeping without insight, duplicate small values.

If duplication starts drifting, normalize the part that actually needs identity.

Schema design is part of the reasoning process, not a prerequisite imposed by the MCP surface.

## Exact update semantics

`update` always sees the current persistent canvas as `.`.

It must emit exactly one JSON value.

Examples of ordinary updates:

`update: '.ideas += [{"text":"Attention behaves like content-addressable memory"}]'`

`update: '.ideas.mamba.status = "central"'`

`update: '.questions += ["Why does hybridization help exact recall?"]'`

`update: 'del(.obsolete)'`

`update: '.items |= map(select(.keep != false))'`

`update: '.observations |= unique'`

`update: '. as $old | {core: $old.notes, archive: $old.discarded}'`

The last example demonstrates an important property: `update` is not merely a patch language. It can reconstruct the whole workspace. Large-scale refactoring is a first-class operation.

If `update` emits zero values, the call fails and the workspace remains unchanged.

If `update` emits multiple values, the call fails and the workspace remains unchanged.

If `update` has a jq compile or runtime error, the call fails and the workspace remains unchanged.

This cardinality rule gives persistence an unambiguous meaning: one call chooses one next canvas.

Be especially careful with jq expressions that stream values.

`update: '.ideas[]'`

is usually wrong because it emits one result per idea and therefore cannot define one next workspace.

If your intention is to replace the whole workspace with the ideas array, use:

`update: '.ideas'`

If your intention is to preserve the root and transform the ideas field, use an assignment or update assignment such as:

`update: '.ideas |= map(...)'`

Know what value your `update` returns. That value becomes reality for subsequent calls.

## Exact query semantics

Only after `update` succeeds and its single output becomes the persistent canvas does `query` run.

The `.` seen by `query` is therefore the updated workspace, not the pre-update workspace.

This ordering is the central RTT-saving property of the interface.

Examples:

Write an idea and immediately retrieve it:

`update: '.ideas.memory = {"text":"Mamba compresses history"}'`

`query: '.ideas.memory'`

Append a candidate and immediately compare all candidates:

`update: '.candidates += [{"name":"hybrid","score":8}]'`

`query: '.candidates | sort_by(-.score)'`

Refactor a structure and immediately inspect only the new keys:

`update: '.ideas |= map({key:.id,value:.}) | from_entries'`

`query: '.ideas | keys'`

Record a decision and return the unresolved issues that should drive the next step:

`update: '.decisions.memory = "use hybrid"'`

`query: '.open_questions'`

Pure read:

`update: '.'`

`query: '.ideas | keys'`

`query` may emit zero, one, or multiple JSON values. All are valid. The tool returns those jq outputs in order and does not interpret their meaning.

`query` is observational. It does not persist its result as another transformation.

If `query` fails after `update` has succeeded, the successful update does not roll back. This tool is explicitly update first, query second. The state transition has already happened; the observation failed. Fix the query and call again with `update: "."` if all you need is to inspect the now-current state.

That distinction is intentional and simple. Do not assume a failed post-update query implies a failed update.

## Query narrowly to save context

A persistent workspace is useful partly because it can become larger than the exact context you need right now.

Do not habitually query `.` just because you can.

Use jq as a lens.

Examples:

`query: 'keys'`

`query: '.ideas | keys'`

`query: '[.ideas[] | select(.status == "unresolved")]'`

`query: '.drafts[-1]'`

`query: '.sections[] | {title,purpose}'`

`query: '[paths(scalars) as $p | {path:$p,value:getpath($p)}]'`

`query: '.. | objects | select(has("uncertainty"))'`

`query: '.relations | group_by(.from) | map({from:.[0].from,count:length})'`

Inspect keys before values. Count before expanding. Filter before returning. Return the slice that will actually change your next reasoning step.

The point of external memory is not to paste all external memory back into the prompt on every turn.

## Recommended pattern for difficult writing

The final prose is linear. The material from which you create it does not have to be.

A useful temporary canvas might contain fragments, candidate explanations, recurring concepts, unresolved questions, comparisons, alternate structures, or drafts. None of those fields are mandatory.

Suppose you are explaining Mamba.

You might initially store several observations:

`update: '.observations = ["Mamba has fixed-size recurrent state","KV cache grows with context","pure recurrent compression can weaken exact recall","hybrid models restore some attention"]'`

Then ask for a compact view:

`query: '.observations'`

Later you notice one idea explains several observations. Instead of merely adding another paragraph, represent the discovery:

`update: '.central = {"text":"The same compression that creates efficiency also creates recall risk","explains":[0,2,3]}'`

Then query what should shape the article:

`query: '{central:.central, observations:.observations}'`

Later you may compare organizations:

`update: '.structures = {"chronological":["Mamba-1","Mamba-2","Mamba-3","Hybrid"],"memory-first":["memory problem","compression","recall weakness","hybrid","architecture evolution"]}'`

`query: '.structures'`

Nothing forces you to keep those structures. If the memory-first view wins, delete the other one, keep both as history, or replace the representation entirely.

The point is to delay premature serialization.

## Recommended pattern for research

Do not let “I encountered this fact” become identical to “this fact already has a place in my final answer”.

You may keep observations loose while the structure is uncertain. You may keep multiple hypotheses. You may explicitly store uncertainties. You may attach sources if source relationships matter. You may later reorganize around mechanisms rather than sources.

For example:

`update: '.hypotheses.h1 = {"idea":"memory bandwidth is the limiting factor","support":[],"problems":[]}'`

`query: '.hypotheses'`

After obtaining evidence elsewhere, update support or problems. The canvas does not verify evidence for you; it gives you an editable place to organize what external tools established.

The canvas is cognitive infrastructure, not an oracle.

## Recommended pattern for design

Keep competing designs alive when the choice is not ready.

`update: '.alternatives += [{"name":"A","advantages":[],"costs":[]},{"name":"B","advantages":[],"costs":[]}]'`

`query: '.alternatives'`

When a new constraint arrives, modify the alternatives and return the comparison in one call.

When the decision becomes clear, pin the conclusion and the reason you will act on, then execute. Do not keep reopening the choice merely because another wording is imaginable.

This is where the original `assume` psychology and the new canvas meet: structure supports deliberation; commitment prevents deliberation from becoming endless motion.

## Preserve alternatives when uncertainty is real

Commitment does not mean pretending uncertainty has vanished.

If several hypotheses remain live because evidence is genuinely insufficient, store several hypotheses. If two article structures are both promising, keep both. If a decision is provisional, represent that fact if it matters.

The discipline is not “always choose immediately”. The discipline is “do not repeatedly reverse a choice without information gain”.

Real uncertainty is state. Anxiety is not evidence.

The canvas can represent the former without being driven by the latter.

## Keep scratch space when classification is premature

Not every fragment deserves an ID or category immediately.

It can be useful to keep:

`{"scratch":[]}`

and move material later when its role becomes clear.

Premature ontology is another form of premature commitment. An idea may begin as an example, become a mechanism, then become the thesis. If you hard-code its role too early, your own representation can bias later reasoning.

Let semantic roles emerge when useful.

## Prefer self-describing structures, but do not worship normalization

Future-you must still be able to understand the canvas.

Meaningful keys are usually easier to revisit than mysterious positional arrays. Objects are useful for addressability. Arrays are useful for order and multiplicity. References are useful when identity matters across several structures.

But do not turn the canvas into a database-design ceremony.

The objective is lower reasoning cost.

If duplication is clearer and harmless, duplicate.

If a canonical identity becomes important, introduce one.

If a relation deserves first-class data, represent it.

If it does not, a nested value may be enough.

## Use whole-structure transformation when local edits preserve the wrong model

One of jq's strongest advantages here is that you are not limited to CRUD.

Sometimes ten local patches are worse than one reconstruction.

For example:

`update: '. as $old | {concepts: ($old.fragments | map({key:.id,value:.}) | from_entries), drafts:$old.drafts, unresolved:$old.questions}'`

This can replace an exploratory schema with a more useful one in a single explicit transformation.

Do not preserve a bad schema because you already invested in it. Sunk cost applies to representations too.

## Use the canvas for callbacks and long-range connections

Long-form reasoning often improves when a later discovery changes the meaning of earlier material.

Suppose you first record “Mamba uses fixed state”. Later you record “exact recall is harder after compression”. Later still you record “hybrid attention restores random access”.

At first these may be separate fragments. Later you can represent the higher-order connection: the same compression mechanism creates both the efficiency advantage and the retrieval weakness, which in turn motivates hybrid attention.

That is more than another note. It is a reorganization of the explanatory structure.

The canvas exists so later understanding can edit earlier organization cheaply.

## Drafts are ordinary data

There is no special draft API.

If a temporary prose rendering helps you think, store it as ordinary JSON if you want to preserve it.

If you want three openings, store three openings.

If you want only a semantic skeleton, store that instead.

If a draft reveals that the underlying structure is wrong, modify the structure rather than polishing a doomed paragraph.

Writing can be used as a diagnostic projection, not only as the final state.

## One workspace means one workspace

There is no workspace selector.

There is no per-call workspace name.

There is no built-in branch or revision in this first version.

If you need separate regions, namespace them inside the JSON yourself:

`{"article":{...},"design":{...}}`

or replace the workspace when the previous material is no longer useful.

Do not assume hidden undo history exists. If a risky transformation deserves a snapshot, preserve whatever region matters inside the canvas before changing it.

Keep that deliberate. Blindly copying the whole workspace into itself on every call can create recursive growth and useless bulk.

## There are no external vars in this version

Both parameters are jq program strings. Embed JSON literals directly using normal jq syntax.

For example:

`update: '.notes += ["The same compression mechanism creates both efficiency and recall limitations."]'`

You are already good at producing JSON and jq literals. Reuse that capability instead of learning another argument injection system.

When storing long text, keep the surrounding jq simple so escaping is the only substantial concern.

## Failure boundaries

Understand the two stages precisely.

If `update` fails to compile, fails at runtime, or does not produce exactly one JSON value, the persistent canvas is unchanged and `query` does not run.

If `update` succeeds, its value is persisted before `query` starts.

If `query` then fails, the update remains persisted. The query failure does not roll back the successful update.

This is not a distributed transaction and there is no revision protocol in this first version. It is a serialized write-then-read operation over one process-local canvas.

Concurrent calls are serialized by the tool so their update→query pairs do not interleave against the single mutable canvas.

## Do not use the canvas as proof

Putting a proposition into JSON does not verify it.

Putting a source label next to a proposition does not prove the source actually says it.

Putting a test plan into JSON does not run the test.

Putting a command into JSON does not execute the command.

Use the correct external capability for observation and action. Then use `assume` to preserve and reorganize the resulting knowledge when persistence helps.

The canvas stores your current working representation of cognition. Reality remains outside it.

## Do not use the canvas ritualistically

For a simple factual response, tiny rewrite, obvious local edit, or other task that fits comfortably in immediate context, just do the task.

The tool is valuable when persistence, non-linearity, restructuring, long-range relationships, comparison, repeated editing, or explicit commitment materially reduce reasoning friction.

Do not create elaborate schemas to justify using the tool.

Do not spend more tokens maintaining the canvas than the task saves.

Do not make every thought explicit merely because persistence is available.

The canvas should carry high-value state, not become a transcript of every mental motion.

## A compact default discipline

When the task benefits from the canvas:

1. Abstract the real structure before editing it.
2. Decide what information is worth persisting.
3. Use `update` to move the canvas to the next useful state.
4. Use `query` in the same call to return only the view needed for the next decision.
5. Execute or investigate outside the canvas as required.
6. Feed materially new results back into the canvas.
7. Refactor the representation when it becomes awkward.
8. Do not reopen a pinned judgment without information gain.
9. Serialize to prose only when prose is the useful next representation.
10. Verify claims and effects with the tools that actually observe reality.

This is guidance, not a mandatory pipeline. The whole reason the canvas is free-form is to let the task determine the structure.

## Final mental model

Think of `assume` as a tiny persistent jq machine attached to your reasoning.

The state is one JSON value.

`update` is the state transition.

`query` is the post-transition observation.

The tool does not know what your JSON means.

That ignorance is a feature.

It means the infrastructure does not dictate how writing, research, planning, design, debugging, synthesis, or creative work must be represented. You can invent a structure, use it, discover its weaknesses, replace it, keep several views, collapse them, or throw them away.

The conversation remains the place where you communicate. The canvas is a place where intermediate structure can live without being forced into conversational order.

And the old `assume` principle still holds at the center: abstract before commitment; once a judgment is good enough to act on, pin it; execute; verify; revise when reality gives you a reason. Do not mistake repeated hesitation for additional evidence.

Abstract → `assume(update, query)` → execute → verify.
