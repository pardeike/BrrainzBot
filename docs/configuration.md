# Configuration

BrrainzBot uses two local files:

- `config.json` for normal settings
- `secrets.json` for sensitive values

The setup wizard writes both files for you.

## What Lives in `config.json`

- installation name
- GitHub repository used for self-update
- AI endpoint base URL and model
- guild-specific IDs
- onboarding policy
- spam-guard policy

## What Lives in `secrets.json`

- Discord bot token
- AI API key

## Commands You Will Use Most

```bash
./brrainzbot print-config
./brrainzbot setup
./brrainzbot doctor
```

## Per-Guild Settings

Each guild has its own:

- guild ID
- welcome channel ID
- spam honeypot channel ID when SpamGuard is enabled
- `NEW` role ID
- `MEMBER` role ID, or the guild ID to use `@everyone`
- owner user ID
- onboarding prompt
- feature toggles

This is important if you run multiple Discord servers with different communities and different confusion cases.

## Good Prompting for the Onboarding AI

Your guild topic prompt should describe:

- who the server is for
- what legitimate users usually want
- what common off-topic arrivals look like
- what obvious spam should be rejected

Keep it short and concrete. One strong paragraph is usually enough.

## Safe Defaults

- `3` attempts
- `10 minute` cooldown
- `24 hour` stale timeout
- owner DMs only for uncertain cases and technical errors
