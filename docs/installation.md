# Installation

## 1. Download a Release

Choose the archive that matches your machine:

- `osx-arm64` for modern Apple Silicon Macs
- `osx-x64` for older Intel Macs
- `linux-x64` for most Linux servers
- `win-x64` for Windows

Extract the archive somewhere stable. Do not place it inside a temporary download folder you clean out regularly.

## 2. Run Setup

```bash
./brrainzbot setup
```

The wizard asks for:

- whether the bot should be enabled now
- your Discord bot token
- your OpenAI-compatible endpoint URL
- your AI model name
- your AI API key
- the Discord IDs for your guild, roles, welcome channel, and spam honeypot channel

It stores:

- normal config in `config.json`
- secrets in `secrets.json`
- session state and logs in the same application directory tree

Safe default:

- the bot starts out disabled unless you explicitly turn it on
- `doctor` can validate everything before the bot touches your servers

## 3. Validate Before Going Live

```bash
./brrainzbot doctor
```

Use this every time you change:

- roles
- channels
- bot token
- AI endpoint
- guild IDs

## 4. Start the Bot

```bash
./brrainzbot run
```

If the bot is still disabled, `run` stays idle and does not connect to Discord. Rerun `setup` when you want to turn it on.

## 5. Change an Existing Install

```bash
./brrainzbot setup
```

This reruns the guided flow with your current values as defaults.

## Optional: Custom Storage Root

You can store the app data somewhere else:

```bash
./brrainzbot setup --root /srv/brrainzbot
./brrainzbot run --root /srv/brrainzbot
```
