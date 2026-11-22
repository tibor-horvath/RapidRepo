<!--
PR title = squash-merge commit = changelog entry. Use Conventional Commits,
e.g. `feat(uow): add explicit transactions`, and add `!` for breaking changes
(`feat!: ...`). See CONTRIBUTING.md.
-->

## Why

<!-- Motivation, and any design decisions or trade-offs a reviewer should know.
Link the issue with "Closes #123" (or "Refs #123"), or delete this line if there is none. -->

## How it was tested

<!-- New or changed tests, or manual steps. "Docs/CI only" is fine. -->

## Checklist

<!-- Tick what applies; strike through or delete items that don't. -->

- [ ] Behaviour that every provider must follow is tested in `RapidRepo.Testing.Conformance`; provider-specific behaviour in that provider's `*.Tests`
- [ ] Public API and behaviour changes are reflected in `docs/` and XML doc comments
- [ ] Breaking changes have `!` in the PR title and a note in `docs/migration/`
- [ ] Changed source-generator snapshots are intended and explained above

## Summary

<!-- Filled in by CodeRabbit; leave the line below as is. -->

@coderabbitai summary
