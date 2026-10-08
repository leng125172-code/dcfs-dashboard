# Contributing

## Workflow

1. Update local `dev`.
2. Create a short-lived `feature/<name>` or `fix/<name>` branch.
3. Keep controllers limited to HTTP concerns and place business rules in `Application` or `Domain`.
4. Add or update tests.
5. Run the complete backend and frontend verification commands from the README.
6. Open a pull request into `dev`.
7. Promote tested releases from `dev` to `master` through a separate pull request.

## Commit policy

Use imperative, scoped commit messages where useful, for example:

```text
feat(auth): add Authentik OIDC callback
fix(gateway): preserve forwarded host port
chore(deps): update .NET patch packages
```

Never commit `.env`, passwords, OIDC client secrets, database dumps, generated runtime configuration, or container data.
