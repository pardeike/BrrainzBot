# BrrainzBot

<img src="docs/assets/illustrations/fluffy-bot.png" alt="BrrainzBot robot" width="120" align="right">

BrrainzBot is a self-hosted Discord bot for two jobs:

- let real people in without public welcome clutter
- clean up common spam before it spreads

It is built for admins who want plain setup, plain docs, and calm day-to-day behavior.

## Quick Start

1. Read the [Discord setup guide](docs/discord-setup.md).
2. Run `brrainzbot setup`.
3. Run `brrainzbot doctor`.
4. Turn one server on with `brrainzbot enable <serverId>`.
5. Run `brrainzbot run`.

If you only manage one server, `brrainzbot enable` and `brrainzbot disable` also work without an ID.

If you want the exact Discord invite link without using the portal generator:

```bash
brrainzbot invite-url --open
```

## What It Does

### Onboarding

- keeps the welcome flow inside `#welcome`
- uses one persistent welcome post, a button, and short prompts
- replies privately instead of cluttering public chat
- promotes approved users into normal server access

### Spam cleanup

- watches a honeypot channel
- catches near-duplicate spam bursts
- deletes spam around the trigger window

## Core Commands

```bash
brrainzbot setup
brrainzbot doctor
brrainzbot status
brrainzbot enable <serverId>
brrainzbot disable <serverId>
brrainzbot invite-url [<serverId>] [--open]
brrainzbot create-member <serverId>
brrainzbot set-members <serverId>
brrainzbot run
brrainzbot print-config
brrainzbot self-update
```

## Docs

- [Home](https://bot.brrai.nz/)
- [Installation](docs/installation.md)
- [Discord setup](docs/discord-setup.md)
- [Configuration](docs/configuration.md)
- [OpenAI-compatible endpoints](docs/openai-compatible.md)
- [Operations](docs/operations.md)

## Development

```bash
dotnet build
dotnet test
```

The docs site uses MkDocs Material:

```bash
pip install -r docs/requirements.txt
mkdocs serve
```

## License

MIT
