# Development Workflow

## Scope

This repository uses `main` as the protected integration branch. Do not commit directly to `main`, push directly to `main`, or deploy from `main` without an approved pull request and explicit deployment authorization.

## Branches

Create work branches from the current `origin/main`:

```bash
git fetch origin
git switch --create feat/short-description origin/main
```

Use `feat/`, `fix/`, `docs/`, `chore/`, or `hotfix/` prefixes. Keep each branch limited to one reviewable purpose.

## Pull Requests

1. Validate the branch locally.
2. Push the feature branch and open a pull request targeting `main`.
3. Link the relevant issue or describe the reason for the change, risks, and validation performed.
4. Wait for the required review approval and the `Build and Test` check to pass.
5. Merge through GitHub. Direct pushes to `main` are prohibited.

GitHub branch protection for `main` must require pull requests, at least one approval, dismissal of stale approvals, and the `Build and Test` status check. Administrators must not bypass these controls except for an explicitly recorded emergency.

## Local Guardrail

Enable the repository hook once on each development machine:

```bash
git config core.hooksPath .githooks
chmod +x .githooks/pre-push scripts/deploy-test-vm.sh
```

The hook rejects a direct push from `main`. GitHub branch protection remains the authoritative server-side control.

## Validation

Run before opening a pull request:

```bash
dotnet restore Orofoods.slnx
dotnet build Orofoods.slnx --no-restore --configuration Release
dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj --no-restore --configuration Release
```

## Test VM Deployment

Deployments are manual only. No GitHub Actions workflow deploys to any environment.

An authorized operator can deploy a reviewed commit to the test VM by supplying the approved host, user, release version, and `--confirm`:

```bash
./scripts/deploy-test-vm.sh --host test-vm.example --user deploy --version 1.2.3 --confirm
```

The script only accepts the `test` environment, builds and tests before publishing, then transfers the artifact and restarts the named service. Never place SSH keys, passwords, connection strings, or VM hostnames in this repository. Record the authorization, commit SHA, release version, and outcome in the deployment ticket.