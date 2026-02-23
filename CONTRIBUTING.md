# Contributing

Thanks for helping improve TryCr4ckP4ss.

## Setup
```bash
git clone <repo-url>
cd TryCr4ckP4ssApp/TryCr4ckP4ss
dotnet restore
dotnet run
```

## Branching
- Create a feature branch from `main`.
- Keep PRs focused and small.

## Coding Rules
- Keep changes local-first and privacy-preserving.
- Avoid introducing telemetry by default.
- Prefer clear, simple code over clever code.
- Do not commit real vault data.

## Before Opening PR
```bash
cd TryCr4ckP4ss
dotnet build
dotnet build -c Release
```

## PR Checklist
- Explain what changed and why.
- Include screenshots/GIFs for UI changes.
- Mention security impact if crypto/storage/auth logic changed.
- Confirm build passes locally.
