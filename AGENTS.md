# Orofoods Agent Instructions

`AGENTS.md` is a policy of instructions for agents working in this repository. It is not a technical security boundary or a replacement for Codex approvals, Windows permissions, GitHub branch protection, Git Credential Manager, or TLS enforcement.

## Repository workflow

- Keep source changes, builds, tests, and local inspection inside the Codex sandbox.
- Read `docs/DEVELOPMENT.md` for the normal branch and pull-request workflow.
- Preserve existing user changes, untracked files, and dirty worktrees.
- Local commits are allowed on feature, fix, documentation, chore, or hotfix branches when the user requests or authorizes them.
- Never commit directly to `main` without explicit user authorization.
- Never push, merge, or deploy directly from `main` without explicit user authorization.

## GitHub operations from the Codex sandbox

- The Windows sandbox cannot use the host user's GitHub credentials reliably.
- For `git fetch`, `git ls-remote`, `git push`, or GitHub operations that fail because of sandbox isolation, use native host-side execution with `sandbox_permissions=require_escalated` when that capability is available and necessary.
- The `sandbox_permissions=require_escalated` value belongs to the Codex tool invocation; it is not a shell argument.
- Request explicit user approval for every remote write, including `git push`, pull-request creation, and merge.
- Do not retry authentication inside the sandbox after a known `SEC_E_NO_CREDENTIALS` failure.
- Do not use `danger-full-access`.
- Do not use `--dangerously-bypass-approvals-and-sandbox`.
- Do not copy tokens, passwords, DPAPI files, GCM stores, GitHub CLI configuration, or credential-manager data into the sandbox.

## Remote-operation policy

- `fetch` and `ls-remote` are permitted only for the configured Orofoods repository.
- `push` is permitted only for a work branch.
- Never push directly to `main`.
- Never force-push, delete remote branches, use mirror pushes, or use `+refspec`.
- Do not merge a pull request as part of the normal workflow. A merge requires separate explicit user authorization.
- GitHub operations are limited to pull-request creation, viewing/listing, and checks unless the user explicitly authorizes another operation.
- Do not use arbitrary `gh api`, authentication, credential, shell, or command-execution operations as a substitute for the approved workflow.

## Credentials and security

- Preserve Git Credential Manager, DPAPI, Schannel, certificate validation, and TLS verification.
- Never disable TLS verification or place credentials in URLs, command arguments, environment variables, source files, or logs.
- Never print or request the value of a token.
- Use the host-side authenticated identity only through the approved execution boundary.

## Reporting

For every host-side GitHub operation, report:

- operation;
- repository and remote;
- branch or ref;
- effective host identity when available;
- approval status;
- exit code and result.

Keep local build/test evidence separate from remote, authenticated, pull-request, and deployment evidence.
