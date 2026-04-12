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

## Prepare a Real `MEMBER` Role

If your server currently uses `@everyone` as the normal member state, you can migrate in two steps:

```bash
./brrainzbot create-member <serverId>
./brrainzbot set-members <serverId>
```

What they do:

- `create-member` creates or fixes a real `MEMBER` role and copies the current `@everyone` channel/category overrides to it
- `set-members` gives that `MEMBER` role to existing non-bot users who are not in active onboarding

If `setup` was your first local run, `create-member` can be the next command before `doctor`.

Onboarding itself is not optional. Spam cleanup is.

Run `create-member` first. Then run `set-members`. Only after that should you remove normal member permissions from `@everyone`.

`create-member` needs both `Manage Roles` and `Manage Channels` on the Discord server.

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

- `MEMBER`

### The bot cannot see `#welcome`

Run `doctor` and check:

- welcome channel ID
- `#welcome` permissions on the channel or its parent category
- role visibility rules
- whether the bot can still send messages there

### New users can still post too early

That is a permission-layout problem, not a runtime bug.

- real `MEMBER` role
- `@everyone` can view but not post

### I want to move from `@everyone` to a real `MEMBER` role

Use:

```bash
./brrainzbot create-member <serverId>
./brrainzbot set-members <serverId>
```

Then move normal member permissions from `@everyone` to `MEMBER`.
