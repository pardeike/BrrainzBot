# Operations

## Start the Bot

```bash
./brrainzbot run
```

The process stays connected to Discord and keeps honoring per-server on/off changes while it is running.

## See Server Status

```bash
./brrainzbot status
```

## Turn a Server On or Off

```bash
./brrainzbot enable <serverId>
./brrainzbot disable <serverId>
```

If you only manage one server, these also work:

```bash
./brrainzbot enable
./brrainzbot disable
```

## Revalidate After Changes

Any time you change:

- roles
- channels
- tokens
- AI endpoint settings

run:

```bash
./brrainzbot doctor
```

## See the Effective Configuration

```bash
./brrainzbot print-config
```

Secrets stay redacted in the output.

## Update the Installed Binary

```bash
./brrainzbot self-update
```

The updater is manual on purpose.

Before it replaces the binary, it shows:

- your current version
- the candidate release
- release notes
- the asset it plans to install

## Logs

The bot writes local JSONL audit logs for:

- uncertain verification cases
- technical failures
- stale-user kicks
- spam-trigger events

## Common Problems

### The bot cannot move users between roles

The bot role is too low in Discord.

Move it above:

- `NEW`
- `MEMBER` if you use the recommended role model

### The bot cannot see `#welcome`

Run `doctor` and check:

- welcome channel ID
- `#welcome` permissions
- role visibility rules

### New users can still post too early

That is a permission-layout problem, not a runtime bug.

Safer model:

- real `MEMBER` role
- `@everyone` can view but not post

Simpler model:

- `@everyone` is the member state
- `NEW` explicitly denies posting until approval
