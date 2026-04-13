# Installation

## Before You Start

Have these ready first:

- a Discord bot token
- your server ID
- your `#welcome` channel ID
- your `MEMBER` role ID if you already have one
- your own Discord user ID for owner DMs
- an AI base URL, model, and API key

If you do not have those yet, start with [Discord setup](discord-setup.md).

Inviting the bot before you enable a server is safe. An installed server stays inactive until you turn it on.

If you already saved the bot token, BrrainzBot can open the exact Discord invite URL for you:

```bash
./brrainzbot invite-url
```

## 1. Download a Release

Choose the archive that matches your machine:

- `osx-arm64` for Apple Silicon Macs
- `osx-x64` for Intel Macs
- `linux-x64` for most Linux servers
- `win-x64` for Windows

Extract it somewhere stable. Do not leave it in a temporary downloads folder.

## 2. Run Setup

```bash
./brrainzbot setup
```

The wizard is built around one server first. Finish one server completely. Add more later if you need them.

Setup asks for:

- your installation name
- the GitHub `owner/repository` used for self-update
- your Discord bot token
- your AI endpoint and model
- one server at a time
- whether spam cleanup should be on for that server

You can leave `MEMBER` blank on the first run if you want BrrainzBot to create or sync that role for you right after setup.

Safe default:

- servers start out off until you turn them on
- rerunning `setup` edits the existing install
- `add` appends one more server without revisiting the install-wide questions

## 3. Create or Sync `MEMBER` if Needed

If you left the `MEMBER` role blank in setup, do this now:

```bash
./brrainzbot create-member <serverId>
```

This can create the role, copy the accessible `@everyone` role and channel settings to it, and update `config.json`.

## 4. Validate Before Go-Live

```bash
./brrainzbot doctor
```

Use this any time you change:

- server IDs
- channel IDs
- role IDs
- Discord bot permissions
- bot token
- AI endpoint settings

## 5. Turn One Server On

```bash
./brrainzbot status
./brrainzbot enable <serverId>
```

If you only manage one server, this also works:

```bash
./brrainzbot enable
```

## 6. Start the Bot

```bash
./brrainzbot run
```

The process stays connected to Discord and picks up per-server on/off changes while it is running.

## 7. Change an Existing Install

```bash
./brrainzbot setup
```

This opens the same guided flow with your current values filled in.

## 8. Add Another Server Later

```bash
./brrainzbot add
```

This skips the install-wide questions and jumps straight to one new server.

## Optional: Custom Storage Root

```bash
./brrainzbot setup --root /srv/brrainzbot
./brrainzbot run --root /srv/brrainzbot
```
