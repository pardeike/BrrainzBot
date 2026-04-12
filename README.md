# BrrainzBot

BrrainzBot is a friendly Discord bot for two jobs that usually get bolted together badly:

- welcoming new people without littering the server
- keeping spam under control without making server setup miserable

It is built for self-hosters who want a polished command-line experience, clear documentation, and a Discord flow that feels human instead of robotic.

## What It Does

### Onboarding / Jail

- Lets new users browse most public channels in read-only mode.
- Keeps the actual verification flow inside `#welcome`.
- Uses one persistent welcome panel, buttons, modals, and ephemeral replies.
- Promotes good users to `MEMBER`.
- Keeps spammy or off-topic users in `NEW`.
- Auto-kicks stale `NEW` users after a configurable timeout.

### SpamGuard

- Watches for honeypot triggers.
- Detects near-duplicate cross-channel spam.
- Deletes old and new messages around a spam trigger.
- Reuses the practical logic from HoneyPotBot.

## Why This Repo Exists

Most Discord bot projects assume you already know:

- how to register a bot
- how Discord permissions actually work
- which roles should grant read access versus write access
- how to wire an OpenAI-compatible endpoint safely

BrrainzBot treats that as a product problem, not a user problem.

## Quick Start

1. Download a release from GitHub.
2. Run `brrainzbot setup`.
3. Follow the prompts.
4. Run `brrainzbot doctor`.
5. Run `brrainzbot run`.

If you are starting from zero, use the full docs:

- [Getting started](docs/index.md)
- [Discord setup guide](docs/discord-setup.md)
- [Configuration guide](docs/configuration.md)
- [OpenAI-compatible endpoint guide](docs/openai-compatible.md)
- [Operations guide](docs/operations.md)

## Command Line

```bash
brrainzbot setup
brrainzbot reconfigure
brrainzbot doctor
brrainzbot print-config
brrainzbot run
brrainzbot self-update
```

## Design Principles

- Friendly outside, serious inside.
- Simple enough for first-time installers.
- Clear enough for professionals who want to move fast.
- Local-first and transparent.
- Manual self-update only. No background version polling.

## Development

```bash
dotnet build
dotnet test
```

The docs site uses MkDocs Material and can be built with:

```bash
pip install -r docs/requirements.txt
mkdocs serve
```

## License

MIT
