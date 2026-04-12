# BrrainzBot

BrrainzBot helps you run a Discord server that feels welcoming to real people and expensive for spammers.

It does this in two layers:

- **Onboarding / Jail** gives new users a clean verification flow in `#welcome` while keeping the rest of the server visible in read-only mode.
- **SpamGuard** deletes classic drive-by spam and repeated cross-channel posts.

This documentation is written for people who do not do Discord bot setup every day.

## The Three Things You Need

1. A Discord server where you can manage roles and channels.
2. A registered Discord bot token.
3. An OpenAI-compatible endpoint and API key for the verification step.

## The Best First Run

1. Read the [Discord setup guide](discord-setup.md).
2. Download a release.
3. Run `brrainzbot setup`.
4. Run `brrainzbot doctor`.
5. Start the bot with `brrainzbot run`.

## The Mental Model

- People join and land in `NEW`.
- They can look around, but they cannot talk in the normal public channels yet.
- They open `#welcome` and click one button.
- The verification happens in ephemeral Discord UI, not in public chat.
- If they fit, they become `MEMBER`.
- If they do not fit, they stay in `NEW`.

## What Makes This Beginner-Friendly

- A setup wizard that creates the local config.
- A reconfigure flow that lets you repair or change an existing install.
- A `doctor` command that validates the setup before you go live.
- Plain language error messages.
- A manual `self-update` command that explains what it is doing before it replaces the binary.
