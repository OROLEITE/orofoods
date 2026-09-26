# Orofoods Development Rules

- Work on a feature, fix, documentation, chore, or hotfix branch. Never commit, push, merge, or deploy from `main` without explicit user authorization.
- Treat pull requests as the only normal path into `main`. Keep changes focused and include the validation performed in the PR description.
- Run the relevant build and tests before proposing integration changes. Do not weaken, skip, or delete tests to make a change pass.
- Do not add credentials, connection strings, access tokens, private keys, production data, or VM addresses to tracked files. Use local secrets or deployment-managed environment variables.
- Do not create, apply, or modify Entity Framework migrations without explicit authorization. Review both `Up` and `Down` migration paths.
- Do not enable automatic deployment. Deployments are manual, test-VM-only operations that require explicit user authorization and the `scripts/deploy-test-vm.sh` confirmation flag.
- Keep production PostgreSQL behavior separate from SQLite test behavior. Do not add automatic schema migration on application startup.
- For WMC homologation, keep file exports manual and do not add automatic retry behavior.