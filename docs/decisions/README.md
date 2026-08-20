# Architecture decision records

ADRs explain consequential, durable choices. They do not replace [`architecture.md`](../architecture.md), which describes how OmniBrille works now.

Use an ADR when a choice changes subsystem authority, state ownership, dependency direction, protocol/persistence/public contracts, renderer/layout strategy, or another costly-to-reverse boundary that history is likely to reopen. Do not write ADRs for ordinary implementation details or every bug fix.

Historical decisions reconstructed after the fact must say **Reconstructed from repository evidence** and cite the supporting commits/source/tests. When a decision changes, add or update its status and link the replacement; do not rewrite history as if the original context never existed.

| ADR | Status | Decision |
| --- | --- | --- |
| [0001](0001-separate-provider-authorities.md) | Accepted | Keep standalone filesystem and connected OmniSorSe authority separate behind application-local provider contracts |
| [0002](0002-bounded-deterministic-scenes.md) | Accepted | Use deterministic bounded Structure/Context/Hybrid scenes and a custom drawing renderer |

## Minimal template

```markdown
# ADR NNNN: Title

Status: Proposed | Accepted | Superseded by ADR NNNN

Reconstructed from repository evidence. <!-- only when applicable -->

## Context
## Decision
## Reasoning
## Consequences
## Rejected alternatives
## Evidence
```
