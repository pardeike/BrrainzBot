# Configuration

BrrainzBot stores two local files:

- `config.json` for normal settings
- `secrets.json` for sensitive values

The setup wizard writes both for you.

## What Lives in `config.json`

- installation name
- GitHub repository used for self-update
- AI endpoint base URL and model
- per-server IDs
- per-server feature toggles
- onboarding text and limits
- spam cleanup settings

## What Lives in `secrets.json`

- Discord bot token
- AI API key

## The Settings You Will Touch Most

```bash
./brrainzbot setup
./brrainzbot status
./brrainzbot doctor
./brrainzbot print-config
```

## Per-Server Settings

Each server has its own:

- server ID
- active on or off state
- welcome channel ID
- honeypot channel ID when spam cleanup is on
- `NEW` role ID
- `MEMBER` role ID
- owner user ID
- server topic prompt
- feature toggles

## Recommended Role Model

Recommended:

- `NEW` is temporary
- `MEMBER` grants normal posting
- `@everyone` does not grant normal posting by itself

Simpler but weaker:

- use `@everyone` as the member state
- set `MemberRoleId` to the server ID
- let approval work by removing `NEW`

The simpler model is fine for small low-risk servers. The real `MEMBER` role is safer.

If you start with the simpler model and later want to migrate, use:

```bash
./brrainzbot create-member <serverId>
./brrainzbot set-members <serverId>
```

## Good Prompting for the Onboarding AI

Your server topic prompt should say:

- who the server is for
- what legitimate users usually want
- what obvious wrong-server arrivals look like
- what obvious spam should be rejected

Keep it short and concrete.

## Safe Defaults

- servers start out off
- `3` attempts
- `10` minute cooldown
- `24` hour stale timeout
- owner DMs only for uncertain cases and technical failures
