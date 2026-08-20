# Engineering learning, freshness, and owner reports

This process applies to substantial runs: architectural or cross-subsystem work, product behavior, high-risk changes, releases, or investigations that produce reusable lessons. Trivial edits do not need a retrospective or retained report.

## Fast loop

```text
task → risk → targeted context → specialist evidence → implementation → validation
     → adversarial review when warranted → documentation freshness
     → retrospective → candidate lessons → owner report
```

Keep the loop lightweight. A candidate lesson is an observation, not permission to add a rule.

## Retrospective

Record concise conclusions, not reasoning transcripts:

- Which assumptions were wrong?
- What architecture had to be rediscovered?
- What information or documentation was missing/stale?
- What caused avoidable exploration or duplicate work?
- What did implementation, review, or tests initially miss?
- Which specialists were useful or unnecessary, and who should have arrived earlier?
- Which validation was unavailable or weaker than expected?
- What could lower future context cost?
- Did a reusable lesson emerge?

For each candidate lesson record: observation, evidence, recurrence/severity, proposed durable form, independent reviewer/evidence, and outcome.

## Controlled promotion

```text
Observation → Candidate lesson → Independent evidence/review → Promotion, retention, or rejection
```

The originating agent may propose but may not unilaterally promote its own observation. Independent support may be a later regression, an existing failing/passing test pair, a separate reviewer reproducing the issue, or consistent code/history evidence.

Prefer promotion in this order:

1. clearer code or names;
2. regression/invariant/performance/accessibility/protocol test;
3. reliable CI/static/documentation check;
4. current architecture or subsystem contract;
5. ADR for an important durable decision;
6. risk/specialist routing;
7. reusable Skill only for a repeated procedure;
8. compact global `AGENTS.md` rule only when broadly necessary.

Outcomes are:

- **Promoted** — name the durable form and evidence.
- **Remain candidate** — useful but evidence, reliability, or recurrence is insufficient.
- **Rejected** — one-off, redundant, too noisy, too costly, or no longer relevant.

## Slow loop and pruning

At a release boundary or when several significant reports accumulate, review candidate/promoted lessons, regressions, failed checks, documentation drift, ADR status, routing outcomes, and context cost.

The slow loop must remove as well as add:

- supersede obsolete ADRs;
- delete or consolidate duplicated current knowledge;
- lower a risk or specialist requirement when evidence no longer supports it;
- remove prose made redundant by an executable check;
- archive historical detail out of default context;
- reject expired candidates;
- simplify `AGENTS.md` when routing can live closer to the subsystem.

Historical reports are evidence, never required default reading.

## Documentation freshness

For every meaningful implementation change, the Documentation role checks:

1. Does [`architecture.md`](../architecture.md) still describe ownership, state, dependencies, and failure behavior?
2. Does the product overview still distinguish implemented, planned, and unverified behavior?
3. Did terminology change in [`glossary.md`](../glossary.md)?
4. Do user flows, Context/Protocol/voice/packaging contracts, performance, accessibility, and validation guidance remain true?
5. Do source/test links and referenced paths still exist?
6. Do Mermaid diagrams still describe actual control/data flow?
7. Did an architectural decision change enough to supersede or add an ADR?
8. Does important intent exist only in the chat?

Run [`build/Test-EngineeringDocs.ps1`](../../build/Test-EngineeringDocs.ps1). It checks required entry points, repository-relative Markdown links, and unclosed Mermaid fences. It cannot validate semantic truth; inspect important diagrams and claims against implementation.

## Significant run reports

Retain a report in [`docs/runs`](../runs/README.md) when a run changes architecture/product boundaries, fixes a serious regression, performs release qualification, creates durable engineering infrastructure, or produces evidence likely to matter during later archaeology.

Do not retain reports for trivial edits. Never rewrite an old report to make its claim agree with a later discovery. Correct current authority, then let a later report/lesson record the discrepancy.

## Owner report template

Use approximately one page and this order.

### What this run was meant to do

Plain-English objective.

### What actually changed

Project/user impact first; only necessary technical context.

### Important technical decisions

For each consequential choice: **Decision → Why → Impact**.

### Validation and confidence

Separate:

- **Verified** — commands, tests, inspections, manual checks actually performed.
- **Not verified** — environment-specific or intentionally omitted validation.
- **Inferred** — supported by evidence but not directly exercised.

Cover build/tests, architecture, performance, UX, accessibility, cross-platform behavior, documentation/Mermaid, and historical counterfactuals where relevant.

### Problems found

Fixed, deferred, newly discovered, and any blocker.

### What the agents learned

Wrong assumptions, rediscovered architecture, useful/unnecessary specialists, routing/context lessons, candidates, promoted/rejected lessons, and durable forms. Do not include hidden chain-of-thought.

### Documentation and diagrams

Created/updated, verified current, known stale/incomplete, diagrams changed, and whether important knowledge remains external.

### Repository state

Repository, branch, HEAD, worktree, pre-existing changes, commits, upstream/push status, and whether schema, protocol, persistence, public interfaces, compatibility assumptions, or intentional product behavior changed.

### Bottom line

Use exactly one status: `Status: Complete`, `Status: Complete with follow-up`, `Status: Partial`, or `Status: Blocked`. State whether the result is safe to build on, remaining uncertainty, and what the owner needs to know next.
